using HSAcademia.Application.DTOs.Attendance;
using HSAcademia.Application.Interfaces;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

public class AttendanceService : IAttendanceService
{
    private readonly AppDbContext _context;
    private const int AutoCloseLookbackDays = 7;

    private readonly FamilyNoticeService _notices;
    private readonly AuditService _audit;

    public AttendanceService(AppDbContext context, FamilyNoticeService notices, AuditService audit)
    {
        _context = context;
        _notices = notices;
        _audit = audit;
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
                     && a.Date >= date.Date && a.Date < date.Date.AddDays(1)
                     && studentIds.Contains(a.StudentId))
            .ToListAsync();

        return students.Select(s => ToRosterDto(s, attendances.FirstOrDefault(a => a.StudentId == s.Id))).ToList();
    }

    public async Task MarkAttendanceAsync(Guid academyId, Guid categoryId, Guid markedByUserId, MarkAttendanceDto dto)
    {
        if (dto.Date.Date > AcademyClock.Today)
            throw new Exception("No se puede registrar asistencia de una fecha futura.");

        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == categoryId && c.AcademyId == academyId);
        if (!categoryExists)
            throw new Exception("Categoría no encontrada.");

        var studentIds = dto.Records.Select(r => r.StudentId).ToList();

        var validStudentIds = await _context.Students
            .Where(s => s.AcademyId == academyId && s.CategoryId == categoryId && studentIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync();

        var existing = await LoadDayMarksAsync(academyId, dto.Date.Date, validStudentIds);
        var marker = await MarkerAsync(markedByUserId);

        foreach (var record in dto.Records)
        {
            if (!validStudentIds.Contains(record.StudentId)) continue;

            if (existing.TryGetValue(record.StudentId, out var existAtt))
            {
                ApplyMark(existAtt, record.Status, AttendanceSource.List, marker);
                existAtt.Notes  = record.Notes;
                _context.Attendances.Update(existAtt);
            }
            else
            {
                var att = new Attendance
                {
                    AcademyId = academyId,
                    StudentId = record.StudentId,
                    Date      = dto.Date.Date,
                    Notes     = record.Notes
                };
                ApplyMark(att, record.Status, AttendanceSource.List, marker, isNew: true);
                _context.Attendances.Add(att);
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

        if (!ev.CategoryId.HasValue && (ev.CategoryIds == null || !ev.CategoryIds.Any()))
            throw new InvalidOperationException("Este evento no tiene una categoría asignada.");

        var categoryIds = new List<Guid>();
        if (ev.CategoryId.HasValue) categoryIds.Add(ev.CategoryId.Value);
        if (ev.CategoryIds != null) categoryIds.AddRange(ev.CategoryIds);
        categoryIds = categoryIds.Distinct().ToList();

        var students = await _context.Students
            .Where(s => s.AcademyId == academyId && categoryIds.Contains(s.CategoryId) && s.IsActive)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync();

        var studentIds = students.Select(s => s.Id).ToList();

        var marks = await LoadSessionMarksAsync(academyId, eventId, ev.StartTime.Date, studentIds);

        return students.Select(s => ToRosterDto(s, marks.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task MarkAttendanceByEventAsync(Guid academyId, Guid markedByUserId, MarkAttendanceByEventDto dto)
    {
        var ev = await _context.Events
            .FirstOrDefaultAsync(e => e.AcademyId == academyId && e.Id == dto.EventId)
            ?? throw new KeyNotFoundException("Evento no encontrado.");

        var isAdmin = await _context.Users.AnyAsync(u => u.Id == markedByUserId && u.Role == UserRole.AcademyAdmin);
        var blocked = AttendanceWindow.BlockReason(ev, isAdmin);
        if (blocked != null) throw new InvalidOperationException(blocked);

        if (ev.AttendanceClosed)
            throw new InvalidOperationException("La asistencia de este evento está cerrada. Solo un administrador puede reabrirla.");

        if (!ev.CategoryId.HasValue && (ev.CategoryIds == null || !ev.CategoryIds.Any()))
            throw new InvalidOperationException("Este evento no tiene categoría asignada.");

        var categoryIds = new List<Guid>();
        if (ev.CategoryId.HasValue) categoryIds.Add(ev.CategoryId.Value);
        if (ev.CategoryIds != null) categoryIds.AddRange(ev.CategoryIds);
        categoryIds = categoryIds.Distinct().ToList();

        var studentIds = dto.Records.Select(r => r.StudentId).ToList();

        var validStudentIds = await _context.Students
            .Where(s => s.AcademyId == academyId
                     && categoryIds.Contains(s.CategoryId)
                     && studentIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync();

        var eventDate = ev.StartTime.Date;
        var existing = await LoadSessionMarksAsync(academyId, dto.EventId, eventDate, validStudentIds);
        var marker = await MarkerAsync(markedByUserId);
        var saved = new List<Attendance>();

        foreach (var record in dto.Records)
        {
            if (!validStudentIds.Contains(record.StudentId)) continue;

            if (existing.TryGetValue(record.StudentId, out var existAtt))
            {
                ApplyMark(existAtt, record.Status, AttendanceSource.List, marker);
                existAtt.EventId = dto.EventId;
                existAtt.Notes  = record.Notes;
                _context.Attendances.Update(existAtt);
                saved.Add(existAtt);
            }
            else
            {
                var att = new Attendance
                {
                    AcademyId = academyId,
                    StudentId = record.StudentId,
                    EventId   = dto.EventId,
                    Date      = eventDate,
                    Notes     = record.Notes
                };
                ApplyMark(att, record.Status, AttendanceSource.List, marker, isNew: true);
                _context.Attendances.Add(att);
                saved.Add(att);
            }
        }
        await _notices.SyncArrivalsAsync(academyId, ev, saved);
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

        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
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
    /// Called by the coach's scanner. Marks the student Present in the given session or, if none
    /// is given, in today's session closest to the current time (or for the day when there is none).
    /// Returns 409 (InvalidOperationException) if already marked in that session.
    /// </summary>
    public async Task<QrScanResultDto> ScanQrAsync(Guid academyId, Guid scannerUserId, QrScanDto dto)
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

        var session = dto.EventId.HasValue
            ? await RequireScannableSessionAsync(academyId, scannerUserId, dto.EventId.Value, student)
            : await FindClosestSessionTodayAsync(academyId, student.CategoryId);
        var date = session?.StartTime.Date ?? AcademyClock.Today;
        var studentName = $"{student.FirstName} {student.LastName}";
        var alreadyMarked = session != null
            ? $"La asistencia de {studentName} ya fue registrada en esta sesión."
            : $"La asistencia de {studentName} ya fue registrada hoy.";
        var marker = await MarkerAsync(scannerUserId);

        Attendance? record;
        if (session != null)
        {
            var marks = await LoadSessionMarksAsync(academyId, session.Id, date, new List<Guid> { student.Id });
            record = marks.GetValueOrDefault(student.Id);
            if (record != null) record.EventId = session.Id;
        }
        else
        {
            record = await _context.Attendances.FirstOrDefaultAsync(a =>
                a.AcademyId == academyId && a.StudentId == student.Id && a.Date == date);
        }

        if (record != null && record.Status != Domain.Enums.AttendanceStatus.Absent)
        {
            await _context.SaveChangesAsync(); // conserva el vínculo con la sesión
            throw new InvalidOperationException(alreadyMarked);
        }

        if (record != null)
        {
            // Figuraba con falta y llegó: el QR lo corrige.
            ApplyMark(record, Domain.Enums.AttendanceStatus.Present, AttendanceSource.Qr, marker);
        }
        else
        {
            record = new Attendance
            {
                AcademyId = academyId,
                StudentId = student.Id,
                EventId   = session?.Id,
                Date      = date,
                Notes     = "Registrado vía QR móvil",
                CreatedAt = DateTime.UtcNow,
            };
            ApplyMark(record, Domain.Enums.AttendanceStatus.Present, AttendanceSource.Qr, marker, isNew: true);
            _context.Attendances.Add(record);
        }

        if (session != null) await _notices.SyncArrivalsAsync(academyId, session, new[] { record });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException) when (session != null)
        {
            // Dos celulares leyeron el mismo carné a la vez: el índice único deja pasar solo uno.
            throw new InvalidOperationException(alreadyMarked);
        }

        return new QrScanResultDto
        {
            AttendanceId = record.Id,
            StudentId    = student.Id,
            StudentName  = studentName,
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
        var windowFrom = new DateTime(AcademyClock.Now.Year, AcademyClock.Now.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                             .AddMonths(-(months - 1));
        var from = windowFrom < enrollMonth ? enrollMonth : windowFrom;
        var to   = DateTime.SpecifyKind(AcademyClock.Today.AddDays(1), DateTimeKind.Utc); // inclusive today

        // ── 1. All events for this student's category in the range ──
        var allEvents = await _context.Events
            .Where(e => e.AcademyId == academyId &&
                        e.StartTime >= from && e.StartTime < to &&
                        !e.IsDeleted)
            .OrderByDescending(e => e.StartTime)
            .ToListAsync();

        var events = allEvents.Where(e =>
            e.CategoryId == student.CategoryId ||
            (e.CategoryIds != null && e.CategoryIds.Contains(student.CategoryId))
        ).ToList();

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
                        a.EventId == null &&
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

            // Find student's own attendance record — prefer EventId match, fall back to a day-only record
            var att = attendances.FirstOrDefault(a => a.EventId == ev.Id)
                   ?? attendances.FirstOrDefault(a => a.EventId == null && a.Date.Date == evDate);

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
                    YearMonth = $"{g.Key.Year}-{g.Key.Month:D2}",
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
            var target = AcademyClock.Now.AddMonths(-i);
            var targetMonth = new DateTime(target.Year, target.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            if (targetMonth < enrollMonth) break;

            var ym = $"{targetMonth.Year}-{targetMonth.Month:D2}";
            if (!grouped.Any(m => m.YearMonth == ym))
            {
                grouped.Add(new MobileAttendanceMonthDto
                {
                    Month     = targetMonth.ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-CL")),
                    YearMonth = ym,
                    Present = 0, Justified = 0, Absent = 0, Total = 0,
                    Records = new List<MobileAttendanceRecordDto>(),
                });
            }
        }

        return grouped
            .OrderByDescending(m => m.YearMonth)
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
                Month     = cur.ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-CL")),
                YearMonth = $"{cur.Year}-{cur.Month:D2}",
                Records   = new List<MobileAttendanceRecordDto>(),
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
        var now  = AcademyClock.Now;
        var from = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = from.AddMonths(1);

        var student = await _context.Students.FindAsync(studentId);
        if (student == null) return new MobileAttendanceSummaryDto();

        var allEvents = await _context.Events
            .Where(e => e.AcademyId == academyId &&
                        e.StartTime >= from && e.StartTime < to &&
                        !e.IsDeleted)
            .ToListAsync();

        var events = allEvents.Where(e =>
            e.CategoryId == student.CategoryId ||
            (e.CategoryIds != null && e.CategoryIds.Contains(student.CategoryId))
        ).ToList();

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
            Total     = events.Count,
        };
    }

    // =========================================================
    // Private helpers
    // =========================================================

    private sealed record Marker(Guid UserId, string? Name);

    private async Task<Marker> MarkerAsync(Guid userId)
    {
        var name = await _context.Users.Where(u => u.Id == userId)
            .Select(u => (u.FirstName + " " + u.LastName).Trim()).FirstOrDefaultAsync();
        return new Marker(userId, name);
    }

    /// <summary>Registra el estado y quién lo marcó. Guardar la lista sin cambiar el estado no borra el origen (ej. QR).</summary>
    private static void ApplyMark(Attendance row, Domain.Enums.AttendanceStatus status, AttendanceSource source, Marker marker, bool isNew = false, DateTime? markedAt = null)
    {
        var now = DateTime.UtcNow;
        row.UpdatedAt = now;
        if (!isNew && row.Status == status) return;
        row.Status = status;
        row.Source = source;
        row.MarkedByUserId = marker.UserId;
        row.MarkedByName = marker.Name;
        // Hora del celular si se marcó sin señal; nunca una hora futura por un reloj mal configurado.
        row.MarkedAt = markedAt.HasValue && markedAt.Value.ToUniversalTime() < now ? markedAt.Value.ToUniversalTime() : now;
    }

    private static StudentAttendanceDto ToRosterDto(Student s, Attendance? att) => new()
    {
        StudentId    = s.Id,
        FirstName    = s.FirstName,
        LastName     = s.LastName,
        AvatarUrl    = null,
        AttendanceId = att?.Id,
        Status       = att?.Status,
        Notes        = att?.Notes,
        MarkedVia    = att?.Source switch { AttendanceSource.Qr => "QR", AttendanceSource.List => "Lista", AttendanceSource.Auto => "Automático", _ => null },
        MarkedByName = att?.MarkedByName,
        MarkedAt     = att?.MarkedAt
    };

    private static List<Guid> CategoriesOf(Event ev) => ev.AllCategoryIds();

    /// <summary>Marca de cada alumno en una sesión: la de esa sesión o, si no hay, un registro del día sin sesión.</summary>
    private async Task<Dictionary<Guid, Attendance>> LoadSessionMarksAsync(Guid academyId, Guid eventId, DateTime date, List<Guid> studentIds)
    {
        var rows = await _context.Attendances
            .Where(a => a.AcademyId == academyId && studentIds.Contains(a.StudentId) &&
                        (a.EventId == eventId || (a.EventId == null && a.Date == date)))
            .ToListAsync();
        return rows.GroupBy(a => a.StudentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.EventId == eventId).First());
    }

    /// <summary>Marca de cada alumno en un día. Un alumno puede tener varias (una por sesión): prioriza la que no tiene sesión.</summary>
    private async Task<Dictionary<Guid, Attendance>> LoadDayMarksAsync(Guid academyId, DateTime date, List<Guid> studentIds)
    {
        var rows = await _context.Attendances
            .Where(a => a.AcademyId == academyId && a.Date == date && studentIds.Contains(a.StudentId))
            .ToListAsync();
        return rows.GroupBy(a => a.StudentId)
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.EventId.HasValue).First());
    }

    private async Task<Event> RequireScannableSessionAsync(Guid academyId, Guid scannerUserId, Guid eventId, Student student)
    {
        var ev = await _context.Events.FirstOrDefaultAsync(e => e.AcademyId == academyId && e.Id == eventId && !e.IsDeleted)
            ?? throw new KeyNotFoundException("Sesión no encontrada.");
        if (ev.AttendanceClosed)
            throw new Exception("La asistencia de esta sesión está cerrada. Solo un administrador puede reabrirla.");

        var categories = CategoriesOf(ev);
        if (!categories.Contains(student.CategoryId))
            throw new Exception($"{student.FirstName} {student.LastName} no pertenece a las categorías de esta sesión.");

        var scanner = await _context.Users.Include(u => u.AssignedCategories)
            .FirstOrDefaultAsync(u => u.Id == scannerUserId && u.AcademyId == academyId)
            ?? throw new Exception("Usuario no encontrado.");
        var isAdmin = scanner.Role == Domain.Enums.UserRole.AcademyAdmin;
        var canMark = isAdmin || ev.TeacherId == scannerUserId || scanner.AssignedCategories.Any(c => categories.Contains(c.Id));
        if (!canMark) throw new Exception("No tienes permiso para esta sesión.");
        var blocked = AttendanceWindow.BlockReason(ev, isAdmin);
        if (blocked != null) throw new Exception(blocked);
        return ev;
    }

    /// <summary>Sesión de hoy ya abierta más cercana a la hora actual; null si hoy no hay sesiones de esa categoría.</summary>
    private async Task<Event?> FindClosestSessionTodayAsync(Guid academyId, Guid categoryId)
    {
        var now = AcademyClock.WallNow;
        var dayStart = DateTime.SpecifyKind(AcademyClock.Today, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);
        var events = await _context.Events
            .Where(e => e.AcademyId == academyId && !e.IsDeleted && !e.AttendanceClosed &&
                        e.StartTime >= dayStart && e.StartTime < dayEnd)
            .ToListAsync();

        var sessions = events
            .Where(e => e.Type is Domain.Enums.EventType.Training or Domain.Enums.EventType.FriendlyMatch or Domain.Enums.EventType.TournamentMatch)
            .Where(e => CategoriesOf(e).Contains(categoryId))
            .ToList();
        if (sessions.Count == 0) return null;

        var open = sessions.Where(e => now >= AttendanceWindow.OpensAt(e)).ToList();
        if (open.Count == 0) throw new Exception(AttendanceWindow.BlockReason(sessions.MinBy(e => e.StartTime)!, isAdmin: false)!);
        return open.MinBy(e => now < e.StartTime ? e.StartTime - now : now > e.EndTime ? now - e.EndTime : TimeSpan.Zero);
    }

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
        if (AcademyClock.WallNow < openAt)
        {
            var remaining = (openAt - AcademyClock.WallNow).TotalMinutes;
            throw new InvalidOperationException(
                $"La lista de asistencia aún no está abierta. " +
                $"Podrás tomarla en {Math.Ceiling(remaining)} minuto(s), " +
                $"15 minutos antes del inicio del evento ({ev.StartTime:HH:mm}).");
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
        Guid academyId, Guid staffUserId, DateTime date, Guid? categoryId = null, Guid? eventId = null, Guid? headquarterId = null)
    {
        var userWithCategories = await _context.Users
            .Include(u => u.AssignedCategories)
            .FirstOrDefaultAsync(u => u.Id == staffUserId && u.AcademyId == academyId);

        if (userWithCategories == null)
            return new List<StudentAttendanceDto>();

        bool isAdmin = userWithCategories.Role == Domain.Enums.UserRole.AcademyAdmin;
        var assignedCategoryIds = userWithCategories.AssignedCategories.Select(c => c.Id).ToList();

        // If an eventId is provided, resolve category IDs from the event
        List<Guid> targetCategoryIds;
        DateTime targetDate = date;

        if (eventId.HasValue)
        {
            var ev = await _context.Events
                .FirstOrDefaultAsync(e => e.AcademyId == academyId && e.Id == eventId.Value && !e.IsDeleted);

            if (ev == null) return new List<StudentAttendanceDto>();

            // Security: Staff can only take attendance if they are the Teacher OR have overlapping assigned categories
            bool isTeacher = ev.TeacherId == staffUserId;

            // Gather all category IDs from the event (single + multi)
            var eventCategoryIds = new List<Guid>();
            if (ev.CategoryId.HasValue) eventCategoryIds.Add(ev.CategoryId.Value);
            if (ev.CategoryIds != null) eventCategoryIds.AddRange(ev.CategoryIds);
            eventCategoryIds = eventCategoryIds.Distinct().ToList();

            // Intersect to check access, but target is ALL event categories
            if (!isAdmin && !isTeacher && !eventCategoryIds.Intersect(assignedCategoryIds).Any())
                return new List<StudentAttendanceDto>(); // No access

            targetCategoryIds = eventCategoryIds;
            targetDate = ev.StartTime.Date;
        }
        else if (categoryId.HasValue)
        {
            if (isAdmin || assignedCategoryIds.Contains(categoryId.Value))
                targetCategoryIds = new List<Guid> { categoryId.Value };
            else
                return new List<StudentAttendanceDto>(); // No access
        }
        else
        {
            if (isAdmin)
            {
                var q = _context.Categories.Where(c => c.AcademyId == academyId);
                if (headquarterId.HasValue)
                    q = q.Where(c => c.HeadquarterId == headquarterId.Value);
                targetCategoryIds = await q.Select(c => c.Id).ToListAsync();
            }
            else
            {
                targetCategoryIds = assignedCategoryIds;
            }
        }

        if (!targetCategoryIds.Any()) return new List<StudentAttendanceDto>();

        var students = await _context.Students
            .Where(s => s.AcademyId == academyId
                     && targetCategoryIds.Contains(s.CategoryId)
                     && s.IsActive)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync();

        var studentIds = students.Select(s => s.Id).ToList();

        // Load attendance: prefer EventId match, fall back to date match
        var attendances = await _context.Attendances
            .Where(a => a.AcademyId == academyId
                     && studentIds.Contains(a.StudentId)
                     && (eventId.HasValue
                            ? (a.EventId == eventId || (a.EventId == null && a.Date >= targetDate && a.Date < targetDate.AddDays(1)))
                            : (a.Date >= targetDate && a.Date < targetDate.AddDays(1))))
            .ToListAsync();

        return students.Select(s =>
        {
            // Prefer event-linked record, fall back to a day-only record
            var att = eventId.HasValue
                ? (attendances.FirstOrDefault(a => a.StudentId == s.Id && a.EventId == eventId)
                   ?? attendances.FirstOrDefault(a => a.StudentId == s.Id))
                : attendances.FirstOrDefault(a => a.StudentId == s.Id);

            return ToRosterDto(s, att);
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

        if (userWithCategories == null)
            throw new Exception("Entrenador no encontrado.");

        bool isAdmin = userWithCategories.Role == Domain.Enums.UserRole.AcademyAdmin;
        var assignedCategoryIds = userWithCategories.AssignedCategories.Select(c => c.Id).ToList();

        List<Guid> targetCategoryIds;
        Event? session = null;

        if (dto.EventId.HasValue)
        {
            var ev = await _context.Events
                .FirstOrDefaultAsync(e => e.AcademyId == academyId && e.Id == dto.EventId.Value && !e.IsDeleted);
            if (ev == null) throw new Exception("Evento no encontrado.");
            session = ev;
            foreach (var record in dto.Records)
            {
                var blocked = AttendanceWindow.BlockReason(ev, isAdmin, record.MarkedAt);
                if (blocked != null) throw new Exception(blocked);
            }
            // Lo marcado a tiempo sin señal entra aunque el cierre automático haya llegado antes.
            var syncedInTime = ev.AttendanceClosedBy == null &&
                               dto.Records.All(r => r.MarkedAt.HasValue && AttendanceWindow.MarkedInTime(ev, r.MarkedAt.Value));
            if (ev.AttendanceClosed && !syncedInTime)
                throw new Exception("La asistencia de este evento está cerrada. Solo un administrador puede reabrirla.");

            bool isTeacher = ev.TeacherId == staffUserId;

            var eventCategoryIds = new List<Guid>();
            if (ev.CategoryId.HasValue) eventCategoryIds.Add(ev.CategoryId.Value);
            if (ev.CategoryIds != null) eventCategoryIds.AddRange(ev.CategoryIds);
            eventCategoryIds = eventCategoryIds.Distinct().ToList();

            if (!isAdmin && !isTeacher && !eventCategoryIds.Intersect(assignedCategoryIds).Any())
                throw new Exception("No tienes permiso para este evento.");

            targetCategoryIds = eventCategoryIds;
        }
        else
        {
            // Sin sesión no hay plazo que aplicar: queda reservado al administrador.
            if (!isAdmin) throw new Exception("Indica la sesión en la que marcas la asistencia.");
            if (dto.Date.Date > AcademyClock.Today) throw new Exception("No se puede registrar asistencia de una fecha futura.");
            targetCategoryIds = await _context.Categories.Where(c => c.AcademyId == academyId).Select(c => c.Id).ToListAsync();
        }

        if (!targetCategoryIds.Any())
            throw new Exception("El entrenador no tiene categorías asignadas para este registro.");

        // Solo los alumnos de sus categorías son válidos (ahora targetCategoryIds)
        var studentIds = dto.Records.Select(r => r.StudentId).ToList();
        var validStudentIds = await _context.Students
            .Where(s => s.AcademyId == academyId
                     && targetCategoryIds.Contains(s.CategoryId)
                     && studentIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync();

        var existing = dto.EventId.HasValue
            ? await LoadSessionMarksAsync(academyId, dto.EventId.Value, dto.Date.Date, validStudentIds)
            : await LoadDayMarksAsync(academyId, dto.Date.Date, validStudentIds);
        var marker = await MarkerAsync(staffUserId);
        var saved = new List<Attendance>();

        foreach (var record in dto.Records)
        {
            if (!validStudentIds.Contains(record.StudentId)) continue;
            var source = string.Equals(record.Via, "QR", StringComparison.OrdinalIgnoreCase) ? AttendanceSource.Qr : AttendanceSource.List;

            if (existing.TryGetValue(record.StudentId, out var existAtt))
            {
                if (record.IfUnmarked) continue;
                ApplyMark(existAtt, record.Status, source, marker, markedAt: record.MarkedAt);
                existAtt.Notes  = record.Notes ?? existAtt.Notes;
                if (dto.EventId.HasValue) existAtt.EventId = dto.EventId;
                _context.Attendances.Update(existAtt);
                saved.Add(existAtt);
            }
            else
            {
                var att = new Attendance
                {
                    AcademyId = academyId,
                    StudentId = record.StudentId,
                    Date      = dto.Date.Date,
                    EventId   = dto.EventId,
                    Notes     = record.Notes
                };
                ApplyMark(att, record.Status, source, marker, isNew: true, markedAt: record.MarkedAt);
                _context.Attendances.Add(att);
                saved.Add(att);
            }
        }
        if (session != null) await _notices.SyncArrivalsAsync(academyId, session, saved);
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
    public async Task ReopenAttendanceAsync(Guid academyId, Guid eventId, Guid reopenedByUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            throw new InvalidOperationException("Indica el motivo de la reapertura (mínimo 5 caracteres).");

        var ev = await _context.Events
            .FirstOrDefaultAsync(e => e.AcademyId == academyId && e.Id == eventId && !e.IsDeleted)
            ?? throw new KeyNotFoundException("Entrenamiento no encontrado.");

        if (!ev.AttendanceClosed)
            throw new InvalidOperationException("La asistencia de este entrenamiento no está cerrada.");

        ev.AttendanceClosed     = false;
        ev.AttendanceClosedAt   = null;
        ev.AttendanceClosedBy   = null;
        ev.AttendanceReopenedAt = DateTime.UtcNow;
        ev.UpdatedAt            = DateTime.UtcNow;
        _audit.Add(academyId, "attendance.reopen", nameof(Event), ev.Id, new { reason = reason.Trim(), reopenedByUserId, ev.Title });
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task<int> AutoCloseExpiredAsync()
    {
        // Solo sesiones recientes: el historial anterior a esta regla no se reescribe.
        var expiredBefore = DateTime.SpecifyKind(AcademyClock.Today.AddDays(-1), DateTimeKind.Utc);
        var notBefore = expiredBefore.AddDays(-AutoCloseLookbackDays);
        var events = (await _context.Events
                .Where(e => !e.IsDeleted && !e.AttendanceClosed && e.AttendanceReopenedAt == null &&
                            e.StartTime >= notBefore && e.StartTime < expiredBefore)
                .ToListAsync())
            .Where(e => e.Type is EventType.Training or EventType.FriendlyMatch or EventType.TournamentMatch)
            .ToList();

        var eventIds = events.Select(e => e.Id).ToList();
        var marked = (await _context.Attendances.Where(a => a.EventId != null && eventIds.Contains(a.EventId.Value))
            .Select(a => a.EventId!.Value).Distinct().ToListAsync()).ToHashSet();

        var closed = 0;
        foreach (var ev in events.Where(e => marked.Contains(e.Id)))
        {
            var categories = CategoriesOf(ev);
            var date = ev.StartTime.Date;
            var dateLocal = DateTime.SpecifyKind(date, DateTimeKind.Utc);
            var students = await _context.Students
                .Where(s => s.AcademyId == ev.AcademyId && s.IsActive && categories.Contains(s.CategoryId) && s.EnrollmentDate <= dateLocal.AddDays(1))
                .Select(s => s.Id)
                .ToListAsync();
            var marks = await LoadSessionMarksAsync(ev.AcademyId, ev.Id, date, students);
            var now = DateTime.UtcNow;
            foreach (var studentId in students.Where(id => !marks.ContainsKey(id)))
            {
                _context.Attendances.Add(new Attendance
                {
                    AcademyId = ev.AcademyId,
                    StudentId = studentId,
                    EventId   = ev.Id,
                    Date      = date,
                    Status    = AttendanceStatus.Absent,
                    Source    = AttendanceSource.Auto,
                    MarkedAt  = now,
                    Notes     = "Sin marcar al vencer el plazo",
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
            ev.AttendanceClosed   = true;
            ev.AttendanceClosedAt = now;
            ev.AttendanceClosedBy = null;
            ev.UpdatedAt          = now;
            await _context.SaveChangesAsync();
            closed++;
        }
        return closed;
    }

    public async Task<List<StaffTrainingSessionDto>> GetStaffTrainingHistoryAsync(
        Guid academyId, Guid staffUserId, int months, Guid? headquarterId = null)
    {
        months = Math.Clamp(months, 1, 24);

        // Resolve staff's assigned category IDs
        var user = await _context.Users
            .Include(u => u.AssignedCategories)
            .FirstOrDefaultAsync(u => u.Id == staffUserId && u.AcademyId == academyId);

        if (user == null)
            return new List<StaffTrainingSessionDto>();

        bool isAdmin = user.Role == Domain.Enums.UserRole.AcademyAdmin;
        var categoryIds = user.AssignedCategories?.Select(c => c.Id).ToList() ?? new List<Guid>();

        var from = new DateTime(AcademyClock.Now.Year, AcademyClock.Now.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                       .AddMonths(-(months - 1));
        var to = new DateTime(AcademyClock.Now.Year, AcademyClock.Now.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                     .AddMonths(1);

        var query = _context.Events
            .Include(e => e.Category)
            .Include(e => e.Headquarter)
            .Where(e => e.AcademyId == academyId &&
                        e.StartTime >= from && e.StartTime < to &&
                        !e.IsDeleted);

        if (isAdmin && headquarterId.HasValue)
            query = query.Where(e => e.HeadquarterId == headquarterId.Value);

        var allEvents = await query.OrderByDescending(e => e.StartTime).ToListAsync();

        var events = allEvents.Where(e =>
            isAdmin ||
            e.TeacherId == staffUserId ||
            (e.CategoryId.HasValue && categoryIds.Contains(e.CategoryId.Value)) ||
            (e.CategoryIds != null && e.CategoryIds.Any(c => categoryIds.Contains(c)))
        ).ToList();

        if (!events.Any()) return new List<StaffTrainingSessionDto>();

        var eventIds = events.Select(e => e.Id).ToList();

        // Collect all distinct category IDs involved in these events
        var eventCategoryIds = events
            .SelectMany(e => (e.CategoryIds ?? new List<Guid>()).Concat(new[] { e.CategoryId ?? Guid.Empty }))
            .Where(c => c != Guid.Empty)
            .Distinct()
            .ToList();

        // Load student counts per category
        var studentCounts = await _context.Students
            .Where(s => s.AcademyId == academyId && eventCategoryIds.Contains(s.CategoryId) && s.IsActive && !s.IsDeleted)
            .GroupBy(s => s.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToListAsync();

        var categoryDict = await _context.Categories
            .Where(c => c.AcademyId == academyId && eventCategoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);

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

        return events.Select(ev =>
        {
            var att  = attendances.FirstOrDefault(a => a.EventId == ev.Id);
            
            var evCats = new List<Guid>();
            if (ev.CategoryId.HasValue) evCats.Add(ev.CategoryId.Value);
            if (ev.CategoryIds != null) evCats.AddRange(ev.CategoryIds);
            evCats = evCats.Distinct().ToList();

            var totalStudents = studentCounts.Where(s => evCats.Contains(s.CategoryId)).Sum(s => s.Count);
            var categoryNames = evCats.Select(c => categoryDict.ContainsKey(c) ? categoryDict[c] : "").Where(n => !string.IsNullOrEmpty(n));
            var categoryNameStr = string.Join(", ", categoryNames);

            return new StaffTrainingSessionDto
            {
                EventId           = ev.Id,
                Title             = ev.Title,
                Date              = ev.StartTime.ToString("yyyy-MM-dd"),
                StartTime         = ev.StartTime.ToString("HH:mm"),
                CategoryId        = ev.CategoryId ?? Guid.Empty,
                CategoryName      = string.IsNullOrEmpty(categoryNameStr) ? "-" : categoryNameStr,
                HeadquarterName   = ev.Headquarter?.Name ?? "-",
                AttendanceClosed  = ev.AttendanceClosed,
                AttendanceClosedAt= ev.AttendanceClosedAt,
                MarkFrom          = AttendanceWindow.OpensAt(ev),
                MarkUntil         = isAdmin ? null : AttendanceWindow.ClosesAt(ev),
                PresentCount     = att?.Present   ?? 0,
                AbsentCount       = att?.Absent    ?? 0,
                JustifiedCount    = att?.Justified ?? 0,
                TotalStudents     = totalStudents,
                AttendanceTaken   = att != null && att.Total > 0,
            };
        }).ToList();
    }
}
