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
    // Private helpers
    // =========================================================

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
