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
        Guid academyId, Guid studentId, int months = 3)
    {
        var student = await _context.Students
            .Include(s => s.Category)
            .FirstOrDefaultAsync(s => s.AcademyId == academyId && s.Id == studentId)
            ?? throw new KeyNotFoundException("Alumno no encontrado.");

        var from = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)
                       .AddMonths(-(months - 1));
        var to   = DateTime.UtcNow.Date.AddDays(1); // inclusive today

        var records = await _context.Attendances
            .Where(a => a.AcademyId == academyId &&
                        a.StudentId == studentId &&
                        a.Date >= from && a.Date < to)
            .OrderByDescending(a => a.Date)
            .ToListAsync();

        var categoryName = student.Category?.Name;

        // Group by Month (Year + Month combination)
        var grouped = records
            .GroupBy(a => new { a.Date.Year, a.Date.Month })
            .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
            .Select(g =>
            {
                var monthRecords = g.Select(a => new MobileAttendanceRecordDto
                {
                    AttendanceId = a.Id,
                    Date         = a.Date.ToString("yyyy-MM-dd"),
                    Status       = MapStatus(a.Status),
                    Category     = categoryName,
                    Notes        = a.Notes,
                }).ToList();

                return new MobileAttendanceMonthDto
                {
                    Month     = new DateTime(g.Key.Year, g.Key.Month, 1)
                                    .ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-CL")),
                    Present   = monthRecords.Count(r => r.Status == "Presente"),
                    Justified = monthRecords.Count(r => r.Status == "Justificado"),
                    Absent    = monthRecords.Count(r => r.Status == "Ausente"),
                    Total     = monthRecords.Count,
                    Records   = monthRecords,
                };
            }).ToList();

        // Ensure at least `months` entries even when no records exist (empty months)
        for (int i = 0; i < months; i++)
        {
            var target = DateTime.UtcNow.AddMonths(-i);
            var label  = new DateTime(target.Year, target.Month, 1)
                             .ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-CL"));
            if (!grouped.Any(m => m.Month.Equals(label, StringComparison.OrdinalIgnoreCase)))
            {
                grouped.Add(new MobileAttendanceMonthDto
                {
                    Month = label, Present = 0, Justified = 0, Absent = 0, Total = 0,
                });
            }
        }

        return grouped
            .OrderByDescending(m =>
                DateTime.ParseExact(m.Month, "MMMM yyyy",
                    new System.Globalization.CultureInfo("es-CL")))
            .ToList();
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
}
