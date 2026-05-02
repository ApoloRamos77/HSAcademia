using HSAcademia.Application.DTOs.Attendance;
using HSAcademia.Application.Interfaces;
using HSAcademia.Domain.Entities;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

public class AttendanceService : IAttendanceService
{
    private readonly AppDbContext _context;

    public AttendanceService(AppDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // Phase 1 — Date-based roll call
    // =========================================================

    public async Task<List<StudentAttendanceDto>> GetAttendanceByCategoryAndDateAsync(
        Guid academyId, Guid categoryId, DateTime date)
    {
        var students = await _context.Students
            .Where(s => s.AcademyId == academyId && s.CategoryId == categoryId && s.IsActive)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync();

        var studentIds = students.Select(s => s.Id).ToList();

        var attendances = await _context.Attendances
            .Where(a => a.AcademyId == academyId
                     && a.Date.Date == date.Date
                     && studentIds.Contains(a.StudentId))
            .ToListAsync();

        return students.Select(s =>
        {
            var att = attendances.FirstOrDefault(a => a.StudentId == s.Id);
            return new StudentAttendanceDto
            {
                StudentId    = s.Id,
                FirstName    = s.FirstName,
                LastName     = s.LastName,
                AvatarUrl    = null,
                AttendanceId = att?.Id,
                Status       = att?.Status,
                Notes        = att?.Notes
            };
        }).ToList();
    }

    public async Task MarkAttendanceAsync(Guid academyId, Guid categoryId, MarkAttendanceDto dto)
    {
        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == categoryId && c.AcademyId == academyId);
        if (!categoryExists)
            throw new Exception("Categoría no encontrada.");

        var studentIds = dto.Records.Select(r => r.StudentId).ToList();

        var validStudentIds = await _context.Students
            .Where(s => s.AcademyId == academyId && s.CategoryId == categoryId && studentIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync();

        var existing = await _context.Attendances
            .Where(a => a.AcademyId == academyId
                     && a.Date.Date == dto.Date.Date
                     && validStudentIds.Contains(a.StudentId))
            .ToDictionaryAsync(a => a.StudentId);

        foreach (var record in dto.Records)
        {
            if (!validStudentIds.Contains(record.StudentId)) continue;

            if (existing.TryGetValue(record.StudentId, out var existAtt))
            {
                existAtt.Status = record.Status;
                existAtt.Notes  = record.Notes;
                _context.Attendances.Update(existAtt);
            }
            else
            {
                _context.Attendances.Add(new Attendance
                {
                    AcademyId = academyId,
                    StudentId = record.StudentId,
                    Date      = dto.Date.Date,
                    Status    = record.Status,
                    Notes     = record.Notes
                });
            }
        }
        await _context.SaveChangesAsync();
    }

    // =========================================================
    // Phase 2 — Event-linked roll call (with 15-min window rule)
    // =========================================================

    public async Task<List<StudentAttendanceDto>> GetAttendanceByEventAsync(Guid academyId, Guid eventId)
    {
        var ev = await _context.Events
            .Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.AcademyId == academyId && e.Id == eventId)
            ?? throw new KeyNotFoundException("Evento no encontrado.");

        EnforceWindowRule(ev);

        if (!ev.CategoryId.HasValue)
            throw new InvalidOperationException("Este evento no tiene una categoría asignada.");

        var students = await _context.Students
            .Where(s => s.AcademyId == academyId && s.CategoryId == ev.CategoryId && s.IsActive)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync();

        var studentIds = students.Select(s => s.Id).ToList();

        var attendances = await _context.Attendances
            .Where(a => a.AcademyId == academyId
                     && a.EventId == eventId
                     && studentIds.Contains(a.StudentId))
            .ToListAsync();

        return students.Select(s =>
        {
            var att = attendances.FirstOrDefault(a => a.StudentId == s.Id);
            return new StudentAttendanceDto
            {
                StudentId    = s.Id,
                FirstName    = s.FirstName,
                LastName     = s.LastName,
                AvatarUrl    = null,
                AttendanceId = att?.Id,
                Status       = att?.Status,
                Notes        = att?.Notes
            };
        }).ToList();
    }

    public async Task MarkAttendanceByEventAsync(Guid academyId, MarkAttendanceByEventDto dto)
    {
        var ev = await _context.Events
            .FirstOrDefaultAsync(e => e.AcademyId == academyId && e.Id == dto.EventId)
            ?? throw new KeyNotFoundException("Evento no encontrado.");

        EnforceWindowRule(ev);

        if (!ev.CategoryId.HasValue)
            throw new InvalidOperationException("Este evento no tiene categoría asignada.");

        var studentIds = dto.Records.Select(r => r.StudentId).ToList();

        var validStudentIds = await _context.Students
            .Where(s => s.AcademyId == academyId
                     && s.CategoryId == ev.CategoryId
                     && studentIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync();

        // Look for existing attendance records by EventId
        var existing = await _context.Attendances
            .Where(a => a.AcademyId == academyId
                     && a.EventId == dto.EventId
                     && validStudentIds.Contains(a.StudentId))
            .ToDictionaryAsync(a => a.StudentId);

        var eventDate = ev.StartTime.Date;

        foreach (var record in dto.Records)
        {
            if (!validStudentIds.Contains(record.StudentId)) continue;

            if (existing.TryGetValue(record.StudentId, out var existAtt))
            {
                existAtt.Status = record.Status;
                existAtt.Notes  = record.Notes;
                _context.Attendances.Update(existAtt);
            }
            else
            {
                _context.Attendances.Add(new Attendance
                {
                    AcademyId = academyId,
                    StudentId = record.StudentId,
                    EventId   = dto.EventId,
                    Date      = eventDate,
                    Status    = record.Status,
                    Notes     = record.Notes
                });
            }
        }
        await _context.SaveChangesAsync();
    }

    // =========================================================
    // Phase 2 — Monthly attendance metrics (70% rule)
    // =========================================================

    public async Task<AttendanceMetricsSummaryDto> GetMonthlyMetricsAsync(
        Guid academyId, Guid categoryId, int year, int month)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.AcademyId == academyId)
            ?? throw new KeyNotFoundException("Categoría no encontrada.");

        var students = await _context.Students
            .Where(s => s.AcademyId == academyId && s.CategoryId == categoryId && s.IsActive)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync();

        if (!students.Any())
            return new AttendanceMetricsSummaryDto
            {
                Year = year, Month = month,
                CategoryId = categoryId, CategoryName = category.Name
            };

        var from = new DateTime(year, month, 1);
        var to   = from.AddMonths(1);

        var studentIds = students.Select(s => s.Id).ToList();

        // All attendance records for this category and month
        var allAttendances = await _context.Attendances
            .Where(a => a.AcademyId == academyId
                     && studentIds.Contains(a.StudentId)
                     && a.Date >= from && a.Date < to)
            .ToListAsync();

        // Total unique sessions = unique dates/events in the period
        var distinctDates = allAttendances
            .Select(a => a.EventId.HasValue ? a.EventId.ToString()! : a.Date.ToString("yyyy-MM-dd"))
            .Distinct()
            .Count();

        // Fallback: if no records yet, count calendar training events for the month
        if (distinctDates == 0)
        {
            distinctDates = await _context.Events
                .CountAsync(e => e.AcademyId == academyId
                              && e.CategoryId == categoryId
                              && e.StartTime >= from
                              && e.StartTime < to
                              && !e.IsDeleted);
        }

        var metrics = students.Select(s =>
        {
            var sAttendances = allAttendances.Where(a => a.StudentId == s.Id).ToList();
            var presentCount = sAttendances.Count(a =>
                a.Status == Domain.Enums.AttendanceStatus.Present ||
                a.Status == Domain.Enums.AttendanceStatus.Late);

            var total   = distinctDates > 0 ? distinctDates : sAttendances.Count;
            var percent = total > 0 ? Math.Round((double)presentCount / total * 100, 1) : 0;

            return new AttendanceMetricDto
            {
                StudentId         = s.Id,
                FullName          = $"{s.FirstName} {s.LastName}",
                TotalSessions     = total,
                PresentCount      = presentCount,
                AttendancePercent = percent,
                IsAlert           = percent < 70
            };
        }).ToList();

        return new AttendanceMetricsSummaryDto
        {
            Year         = year,
            Month        = month,
            CategoryId   = categoryId,
            CategoryName = category.Name,
            Students     = metrics
        };
    }

    // =========================================================
    // Phase 3 — Mobile App: QR Scan
    // =========================================================

    /// <summary>
    /// Called by the coach's scanner. Looks up the student by their UserId (GuardianId
    /// is the link between User and Student). Marks them Present for today.
    /// Returns 409 (InvalidOperationException) if already scanned today.
    /// </summary>
    public async Task<QrScanResultDto> ScanQrAsync(Guid academyId, QrScanDto dto)
    {
        // A student's QR embeds their own UserId — but students don't have a User account
        // in the current model. The QR instead embeds the Student.Id directly (field "u").
        var student = await _context.Students
            .Include(s => s.Category)
            .FirstOrDefaultAsync(s =>
                s.AcademyId == academyId &&
                s.Id == dto.StudentUserId &&
                s.IsActive && !s.IsDeleted)
            ?? throw new KeyNotFoundException("Alumno no encontrado en esta academia.");

        var today = DateTime.UtcNow.Date;

        // Duplicate check — already scanned today?
        var existing = await _context.Attendances
            .FirstOrDefaultAsync(a =>
                a.AcademyId == academyId &&
                a.StudentId == student.Id &&
                a.Date == today);

        if (existing is not null)
            throw new InvalidOperationException(
                $"La asistencia de {student.FirstName} {student.LastName} ya fue registrada hoy.");

        var record = new Attendance
        {
            AcademyId = academyId,
            StudentId = student.Id,
            Date      = today,
            Status    = Domain.Enums.AttendanceStatus.Present,
            Notes     = "Registrado vía QR móvil",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _context.Attendances.Add(record);
        await _context.SaveChangesAsync();

        return new QrScanResultDto
        {
            AttendanceId = record.Id,
            StudentName  = $"{student.FirstName} {student.LastName}",
            Category     = student.Category?.Name ?? "-",
            Status       = "Registrado",
            MarkedAt     = DateTime.UtcNow,
        };
    }

    // =========================================================
    // Phase 3 — Mobile App: Student's own history & summary
    // =========================================================

    /// <summary>
    /// Returns up to `months` calendar months of attendance records grouped for the
    /// mobile timeline. Months are returned newest-first.
    /// </summary>
    public async Task<List<MobileAttendanceMonthDto>> GetMyAttendanceHistoryAsync(
        Guid academyId, Guid studentId, int months = 60)
    {
        months = Math.Clamp(months, 1, 60);

        var student = await _context.Students
            .Include(s => s.Category)
            .FirstOrDefaultAsync(s => s.AcademyId == academyId && s.Id == studentId)
            ?? throw new KeyNotFoundException("Alumno no encontrado.");

        if (student.CategoryId == Guid.Empty)
            return new List<MobileAttendanceMonthDto>();

        // Earliest possible date = enrollment date
        var enrollDate = student.PaymentStartDate ?? student.EnrollmentDate;
        var enrollMonth = new DateTime(enrollDate.Year, enrollDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Window: from enrollment OR (now - months), whichever is later
        var windowFrom = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                             .AddMonths(-(months - 1));
        var from = windowFrom < enrollMonth ? enrollMonth : windowFrom;
        var to   = DateTime.UtcNow.Date.AddDays(1); // inclusive today

        // ── 1. All events for this student's category in the range ──
        var events = await _context.Events
            .Where(e => e.AcademyId == academyId &&
                        e.CategoryId == student.CategoryId &&
                        e.StartTime >= from && e.StartTime < to &&
                        !e.IsDeleted)
            .OrderByDescending(e => e.StartTime)
            .ToListAsync();

        if (!events.Any())
        {
            // No events → still return empty-record months since enrollment
            return BuildEmptyMonths(from, DateTime.UtcNow, student.Category?.Name);
        }

        // ── 2. Attendance records for this student in the range ──
        var attendances = await _context.Attendances
            .Where(a => a.AcademyId == academyId &&
                        a.StudentId == studentId &&
                        a.Date >= from && a.Date < to)
            .ToListAsync();

        // ── 3. For each event, check if attendance was taken at all ──
        //    (any student in that category for that date)
        var eventIds = events.Select(e => e.Id).ToList();
        var eventDates = events.Select(e => e.StartTime.Date).Distinct().ToList();

        // Count records per event (or per date if no EventId)
        var takenEventIds = await _context.Attendances
            .Where(a => a.AcademyId == academyId &&
                        eventIds.Contains(a.EventId ?? Guid.Empty))
            .Select(a => a.EventId)
            .Distinct()
            .ToListAsync();

        // Also check by date+category for records saved without EventId
        var takenDates = await _context.Attendances
            .Include(a => a.Student)
            .Where(a => a.AcademyId == academyId &&
                        a.Student!.CategoryId == student.CategoryId &&
                        eventDates.Contains(a.Date.Date))
            .Select(a => a.Date.Date)
            .Distinct()
            .ToListAsync();

        var categoryName = student.Category?.Name;

        // ── 4. Build one record per event ──
        var records = events.Select(ev =>
        {
            var evDate = ev.StartTime.Date;

            // Find student's own attendance record — prefer EventId match, fall back to date match
            var att = attendances.FirstOrDefault(a => a.EventId == ev.Id)
                   ?? attendances.FirstOrDefault(a => a.Date.Date == evDate);

            // Was attendance taken for this session by anyone?
            bool taken = ev.AttendanceClosed ||
                         (takenEventIds.Contains(ev.Id)) ||
                         takenDates.Contains(evDate);

            string status;
            if (att != null)
                status = MapStatus(att.Status);
            else if (!taken)
                status = "Pendiente";   // coach hasn't marked anyone yet
            else
                status = "Ausente";     // coach marked others but not this student

            return new
            {
                DateObj = evDate,
                Record  = new MobileAttendanceRecordDto
                {
                    AttendanceId = att?.Id ?? Guid.Empty,
                    Date         = evDate.ToString("yyyy-MM-dd"),
                    Status       = status,
                    Category     = categoryName,
                    Notes        = att?.Notes ?? ev.Title,
                }
            };
        }).ToList();

        // ── 5. Group by month ──
        var grouped = records
            .GroupBy(r => new { r.DateObj.Year, r.DateObj.Month })
            .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
            .Select(g =>
            {
                var monthRecords = g.Select(x => x.Record).ToList();
                return new MobileAttendanceMonthDto
                {
                    Month     = new DateTime(g.Key.Year, g.Key.Month, 1)
                                    .ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-CL")),
                    Present   = monthRecords.Count(r => r.Status == "Presente" || r.Status == "Tardanza"),
                    Justified = monthRecords.Count(r => r.Status == "Justificado"),
                    Absent    = monthRecords.Count(r => r.Status == "Ausente"),
                    Total     = monthRecords.Count,
                    Records   = monthRecords,
                };
            }).ToList();

        // ── 6. Pad with empty months that had no events ──
        for (int i = 0; i < months; i++)
        {
            var target = DateTime.UtcNow.AddMonths(-i);
            var targetMonth = new DateTime(target.Year, target.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            if (targetMonth < enrollMonth) break;

            var label = targetMonth.ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-CL"));
            if (!grouped.Any(m => m.Month.Equals(label, StringComparison.OrdinalIgnoreCase)))
            {
                grouped.Add(new MobileAttendanceMonthDto
                {
                    Month = label, Present = 0, Justified = 0, Absent = 0, Total = 0,
                    Records = new List<MobileAttendanceRecordDto>(),
                });
            }
        }

        return grouped
            .OrderByDescending(m =>
                DateTime.ParseExact(m.Month, "MMMM yyyy",
                    new System.Globalization.CultureInfo("es-CL")))
            .ToList();
    }

    private static List<MobileAttendanceMonthDto> BuildEmptyMonths(
        DateTime from, DateTime to, string? categoryName)
    {
        var result = new List<MobileAttendanceMonthDto>();
        var cur = new DateTime(to.Year, to.Month, 1);
        while (cur >= new DateTime(from.Year, from.Month, 1))
        {
            result.Add(new MobileAttendanceMonthDto
            {
                Month = cur.ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-CL")),
                Records = new List<MobileAttendanceRecordDto>(),
            });
            cur = cur.AddMonths(-1);
        }
        return result;
    }


    /// <summary>
    /// Compact summary for the current calendar month (dashboard widget).
    /// </summary>
    public async Task<MobileAttendanceSummaryDto> GetMyAttendanceSummaryAsync(
        Guid academyId, Guid studentId)
    {
        var now  = DateTime.UtcNow;
        var from = new DateTime(now.Year, now.Month, 1);
        var to   = from.AddMonths(1);

        var records = await _context.Attendances
            .Where(a => a.AcademyId == academyId &&
                        a.StudentId == studentId &&
                        a.Date >= from && a.Date < to)
            .ToListAsync();

        return new MobileAttendanceSummaryDto
        {
            Present   = records.Count(a => a.Status == Domain.Enums.AttendanceStatus.Present ||
                                           a.Status == Domain.Enums.AttendanceStatus.Late),
            Justified = records.Count(a => a.Status == Domain.Enums.AttendanceStatus.Excused),
            Absent    = records.Count(a => a.Status == Domain.Enums.AttendanceStatus.Absent),
            Total     = records.Count,
        };
    }

    // =========================================================
    // Private helpers
    // =========================================================

    private static string MapStatus(Domain.Enums.AttendanceStatus status) => status switch
    {
        Domain.Enums.AttendanceStatus.Present   => "Presente",
        Domain.Enums.AttendanceStatus.Absent    => "Ausente",
        Domain.Enums.AttendanceStatus.Excused => "Justificado",
        Domain.Enums.AttendanceStatus.Late      => "Tardanza",
        _ => "Desconocido",
    };

    /// <summary>
    /// Enforces: roll call can only be opened 15 minutes before event start (or after it starts).
    /// </summary>
    private static void EnforceWindowRule(Domain.Entities.Event ev)
    {
        var openAt = ev.StartTime.AddMinutes(-15);
        if (DateTime.UtcNow < openAt)
        {
            var remaining = (openAt - DateTime.UtcNow).TotalMinutes;
            throw new InvalidOperationException(
                $"La lista de asistencia aún no está abierta. " +
                $"Podrás tomarla en {Math.Ceiling(remaining)} minuto(s), " +
                $"15 minutos antes del inicio del evento ({ev.StartTime:HH:mm} UTC).");
        }
    }

    // =========================================================
    // Staff Mobile: asistencia de sus alumnos
    // =========================================================

    /// <summary>
    /// Returns all active students from the Staff's assigned categories,
    /// with their attendance status for the specified date.
    /// </summary>
    public async Task<List<StudentAttendanceDto>> GetMyStudentsAttendanceAsync(
        Guid academyId, Guid staffUserId, DateTime date)
    {
        var userWithCategories = await _context.Users
            .Include(u => u.AssignedCategories)
            .FirstOrDefaultAsync(u => u.Id == staffUserId && u.AcademyId == academyId);

        if (userWithCategories == null || !userWithCategories.AssignedCategories.Any())
            return new List<StudentAttendanceDto>();

        var assignedCategoryIds = userWithCategories.AssignedCategories.Select(c => c.Id).ToList();

        var students = await _context.Students
            .Where(s => s.AcademyId == academyId
                     && assignedCategoryIds.Contains(s.CategoryId)
                     && s.IsActive)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync();

        var studentIds = students.Select(s => s.Id).ToList();

        var attendances = await _context.Attendances
            .Where(a => a.AcademyId == academyId
                     && a.Date.Date == date.Date
                     && studentIds.Contains(a.StudentId))
            .ToListAsync();

        return students.Select(s =>
        {
            var att = attendances.FirstOrDefault(a => a.StudentId == s.Id);
            return new StudentAttendanceDto
            {
                StudentId    = s.Id,
                FirstName    = s.FirstName,
                LastName     = s.LastName,
                AvatarUrl    = null,
                AttendanceId = att?.Id,
                Status       = att?.Status,
                Notes        = att?.Notes
            };
        }).ToList();
    }

    /// <summary>
    /// Saves attendance records for the Staff's students.
    /// Only allows marking students that belong to their assigned categories.
    /// </summary>
    public async Task SaveMyStudentsAttendanceAsync(
        Guid academyId, Guid staffUserId, MarkAttendanceDto dto)
    {
        var userWithCategories = await _context.Users
            .Include(u => u.AssignedCategories)
            .FirstOrDefaultAsync(u => u.Id == staffUserId && u.AcademyId == academyId);

        if (userWithCategories == null || !userWithCategories.AssignedCategories.Any())
            throw new Exception("El entrenador no tiene categorías asignadas.");

        var assignedCategoryIds = userWithCategories.AssignedCategories.Select(c => c.Id).ToList();

        // Solo los alumnos de sus categorías son válidos
        var studentIds = dto.Records.Select(r => r.StudentId).ToList();
        var validStudentIds = await _context.Students
            .Where(s => s.AcademyId == academyId
                     && assignedCategoryIds.Contains(s.CategoryId)
                     && studentIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync();

        var existing = await _context.Attendances
            .Where(a => a.AcademyId == academyId
                     && a.Date.Date == dto.Date.Date
                     && validStudentIds.Contains(a.StudentId))
            .ToDictionaryAsync(a => a.StudentId);

        foreach (var record in dto.Records)
        {
            if (!validStudentIds.Contains(record.StudentId)) continue;

            if (existing.TryGetValue(record.StudentId, out var existAtt))
            {
                existAtt.Status = record.Status;
                existAtt.Notes  = record.Notes;
                _context.Attendances.Update(existAtt);
            }
            else
            {
                _context.Attendances.Add(new Attendance
                {
                    AcademyId = academyId,
                    StudentId = record.StudentId,
                    Date      = dto.Date.Date,
                    Status    = record.Status,
                    Notes     = record.Notes
                });
            }
        }
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task<Guid?> ResolveStudentIdAsync(Guid academyId, Guid userId)
    {
        var id = await _context.Students
            .Where(s => s.AcademyId == academyId && !s.IsDeleted &&
                        (s.UserId == userId || s.GuardianId == userId))
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync();
        return id;
    }

    /// <inheritdoc/>
    public async Task CloseAttendanceAsync(Guid academyId, Guid eventId, Guid closedByUserId)
    {
        var ev = await _context.Events
            .FirstOrDefaultAsync(e => e.AcademyId == academyId && e.Id == eventId && !e.IsDeleted)
            ?? throw new KeyNotFoundException("Entrenamiento no encontrado.");

        if (ev.AttendanceClosed)
            throw new InvalidOperationException("La asistencia de este entrenamiento ya fue cerrada.");

        ev.AttendanceClosed   = true;
        ev.AttendanceClosedAt = DateTime.UtcNow;
        ev.AttendanceClosedBy = closedByUserId;
        ev.UpdatedAt          = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task ReopenAttendanceAsync(Guid academyId, Guid eventId)
    {
        var ev = await _context.Events
            .FirstOrDefaultAsync(e => e.AcademyId == academyId && e.Id == eventId && !e.IsDeleted)
            ?? throw new KeyNotFoundException("Entrenamiento no encontrado.");

        if (!ev.AttendanceClosed)
            throw new InvalidOperationException("La asistencia de este entrenamiento no está cerrada.");

        ev.AttendanceClosed   = false;
        ev.AttendanceClosedAt = null;
        ev.AttendanceClosedBy = null;
        ev.UpdatedAt          = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task<List<StaffTrainingSessionDto>> GetStaffTrainingHistoryAsync(
        Guid academyId, Guid staffUserId, int months)
    {
        months = Math.Clamp(months, 1, 24);

        // Resolve staff's assigned category IDs
        var user = await _context.Users
            .Include(u => u.AssignedCategories)
                .ThenInclude(c => c.Headquarter)
            .FirstOrDefaultAsync(u => u.Id == staffUserId && u.AcademyId == academyId);

        if (user == null || !user.AssignedCategories.Any())
            return new List<StaffTrainingSessionDto>();

        var categoryIds = user.AssignedCategories.Select(c => c.Id).ToList();

        var from = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                       .AddMonths(-(months - 1));
        var to   = DateTime.UtcNow.Date.AddDays(1);

        // All training events for these categories in range
        var events = await _context.Events
            .Include(e => e.Category)
            .Include(e => e.Headquarter)
            .Where(e => e.AcademyId == academyId &&
                        e.CategoryId.HasValue &&
                        categoryIds.Contains(e.CategoryId!.Value) &&
                        e.StartTime >= from && e.StartTime < to &&
                        !e.IsDeleted)
            .OrderByDescending(e => e.StartTime)
            .ToListAsync();

        if (!events.Any()) return new List<StaffTrainingSessionDto>();

        var eventIds = events.Select(e => e.Id).ToList();

        // Load student counts per category
        var studentCounts = await _context.Students
            .Where(s => s.AcademyId == academyId && categoryIds.Contains(s.CategoryId) && s.IsActive && !s.IsDeleted)
            .GroupBy(s => s.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToListAsync();

        // Load attendance summaries per event
        var attendances = await _context.Attendances
            .Where(a => a.AcademyId == academyId && eventIds.Contains(a.EventId ?? Guid.Empty))
            .GroupBy(a => a.EventId)
            .Select(g => new
            {
                EventId = g.Key,
                Present   = g.Count(a => a.Status == Domain.Enums.AttendanceStatus.Present || a.Status == Domain.Enums.AttendanceStatus.Late),
                Absent    = g.Count(a => a.Status == Domain.Enums.AttendanceStatus.Absent),
                Justified = g.Count(a => a.Status == Domain.Enums.AttendanceStatus.Excused),
                Total     = g.Count()
            })
            .ToListAsync();

        var culture = new System.Globalization.CultureInfo("es-CL");

        return events.Select(ev =>
        {
            var att  = attendances.FirstOrDefault(a => a.EventId == ev.Id);
            var sCnt = studentCounts.FirstOrDefault(s => s.CategoryId == ev.CategoryId!.Value);
            return new StaffTrainingSessionDto
            {
                EventId           = ev.Id,
                Title             = ev.Title,
                Date              = ev.StartTime.ToString("yyyy-MM-dd"),
                StartTime         = ev.StartTime.ToString("HH:mm"),
                CategoryName      = ev.Category?.Name ?? "-",
                HeadquarterName   = ev.Headquarter?.Name ?? "-",
                AttendanceClosed  = ev.AttendanceClosed,
                AttendanceClosedAt= ev.AttendanceClosedAt,
                PresentCount      = att?.Present   ?? 0,
                AbsentCount       = att?.Absent    ?? 0,
                JustifiedCount    = att?.Justified ?? 0,
                TotalStudents     = sCnt?.Count    ?? 0,
                AttendanceTaken   = att != null && att.Total > 0,
            };
        }).ToList();
    }
}
