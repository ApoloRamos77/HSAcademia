using HSAcademia.Application.DTOs.Attendance;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Asistencia de entrenadores por sesión. La registra el administrador o un coordinador con permiso,
/// este último solo en sus categorías asignadas y nunca sobre sí mismo.
/// </summary>
public class StaffAttendanceService
{
    private const int MinutesBeforeStart = 15;
    private const string OwnSessionMessage = "Otra persona debe registrar tu propia asistencia.";

    private readonly AppDbContext _db;

    public StaffAttendanceService(AppDbContext db) => _db = db;

    private sealed record Scope(Guid UserId, string Name, bool IsAdmin, HashSet<Guid> Categories)
    {
        public bool Supervises(Event ev) => IsAdmin || ev.AllCategoryIds().Any(Categories.Contains);
    }

    private async Task<Scope> ScopeAsync(Guid academyId, Guid userId)
    {
        var user = await _db.Users.AsNoTracking().Include(u => u.AssignedCategories)
            .FirstOrDefaultAsync(u => u.Id == userId && u.AcademyId == academyId)
            ?? throw new Exception("Usuario no encontrado.");
        return new Scope(userId, $"{user.FirstName} {user.LastName}".Trim(), user.Role == UserRole.AcademyAdmin,
            user.AssignedCategories.Select(c => c.Id).ToHashSet());
    }

    private static bool IsSession(Event e) =>
        e.Type is EventType.Training or EventType.FriendlyMatch or EventType.TournamentMatch;

    private static bool HasStarted(Event ev) => ev.StartTime <= AcademyClock.WallNow.AddMinutes(MinutesBeforeStart);

    private Task<Dictionary<Guid, string>> NamesAsync(Guid academyId, IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList();
        return _db.Users.AsNoTracking().Where(u => u.AcademyId == academyId && list.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.FirstName + " " + u.LastName).Trim());
    }

    public async Task<List<StaffSessionDto>> GetSessionsAsync(Guid academyId, Guid userId, DateTime date)
    {
        var scope = await ScopeAsync(academyId, userId);
        var start = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var end = start.AddDays(1);
        var events = (await _db.Events.AsNoTracking().Include(e => e.Headquarter)
                .Where(e => e.AcademyId == academyId && !e.IsDeleted && e.StartTime >= start && e.StartTime < end)
                .OrderBy(e => e.StartTime)
                .ToListAsync())
            .Where(IsSession).Where(scope.Supervises).ToList();

        var eventIds = events.Select(e => e.Id).ToList();
        var marks = await _db.StaffAttendances.AsNoTracking()
            .Where(a => a.AcademyId == academyId && a.EventId != null && eventIds.Contains(a.EventId.Value))
            .ToDictionaryAsync(a => a.EventId!.Value);
        var categoryNames = await _db.Categories.AsNoTracking().Where(c => c.AcademyId == academyId)
            .ToDictionaryAsync(c => c.Id, c => c.Name);
        var names = await NamesAsync(academyId, events.Where(e => e.TeacherId.HasValue).Select(e => e.TeacherId!.Value)
            .Concat(marks.Values.Select(m => m.StaffId))
            .Concat(marks.Values.Where(m => m.SubstituteStaffId.HasValue).Select(m => m.SubstituteStaffId!.Value)));

        return events.Select(ev =>
        {
            var mark = marks.GetValueOrDefault(ev.Id);
            var staffId = mark?.StaffId ?? ev.TeacherId;
            string? blocked = null;
            if (!HasStarted(ev)) blocked = $"Se podrá marcar desde {MinutesBeforeStart} minutos antes del inicio.";
            else if (!scope.IsAdmin && staffId == userId) blocked = OwnSessionMessage;

            return new StaffSessionDto
            {
                EventId = ev.Id,
                Title = ev.Title,
                Date = ev.StartTime.ToString("yyyy-MM-dd"),
                StartTime = ev.StartTime.ToString("HH:mm"),
                CategoryName = string.Join(", ", ev.AllCategoryIds().Select(id => categoryNames.GetValueOrDefault(id)).Where(n => n != null)),
                HeadquarterName = ev.Headquarter?.Name ?? "-",
                StaffId = staffId,
                StaffName = staffId.HasValue ? names.GetValueOrDefault(staffId.Value) : null,
                Status = mark?.Status,
                SubstituteStaffId = mark?.SubstituteStaffId,
                SubstituteName = mark?.SubstituteStaffId is Guid sub ? names.GetValueOrDefault(sub) : null,
                Notes = mark?.Notes,
                MarkedByName = mark?.MarkedByName,
                MarkedAt = mark?.MarkedAt,
                BlockedReason = blocked
            };
        }).ToList();
    }

    public Task<List<StaffOptionDto>> GetTrainersAsync(Guid academyId) =>
        _db.Users.AsNoTracking()
            .Where(u => u.AcademyId == academyId && u.Role == UserRole.Staff && u.Status == UserStatus.Active)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Select(u => new StaffOptionDto { Id = u.Id, Name = (u.FirstName + " " + u.LastName).Trim() })
            .ToListAsync();

    public async Task SaveAsync(Guid academyId, Guid userId, List<StaffSessionMarkDto> records)
    {
        if (records.Count == 0) throw new Exception("No hay marcas para guardar.");
        var scope = await ScopeAsync(academyId, userId);
        var eventIds = records.Select(r => r.EventId).Distinct().ToList();
        var events = await _db.Events.AsNoTracking()
            .Where(e => e.AcademyId == academyId && !e.IsDeleted && eventIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id);
        var marks = await _db.StaffAttendances
            .Where(a => a.AcademyId == academyId && a.EventId != null && eventIds.Contains(a.EventId.Value))
            .ToDictionaryAsync(a => a.EventId!.Value);
        var trainers = (await GetTrainersAsync(academyId)).Select(t => t.Id).ToHashSet();

        foreach (var record in records)
        {
            if (!events.TryGetValue(record.EventId, out var ev)) throw new KeyNotFoundException("Sesión no encontrada.");
            if (!IsSession(ev)) throw new Exception("Este evento no lleva asistencia de entrenador.");
            if (!scope.Supervises(ev)) throw new Exception($"\"{ev.Title}\" no pertenece a tus categorías asignadas.");
            if (!HasStarted(ev)) throw new Exception($"\"{ev.Title}\" aún no inicia.");
            if (!Enum.IsDefined(record.Status)) throw new Exception("El estado de asistencia no es válido.");

            marks.TryGetValue(record.EventId, out var mark);
            if (record.StaffId.HasValue && !trainers.Contains(record.StaffId.Value)) throw new Exception("El entrenador indicado no es válido.");
            var staffId = ev.TeacherId ?? record.StaffId ?? mark?.StaffId
                ?? throw new Exception($"Indica qué entrenador dictó \"{ev.Title}\".");

            var replaced = record.Status is AttendanceStatus.Absent or AttendanceStatus.Excused;
            var substitute = replaced ? record.SubstituteStaffId : null;
            if (substitute.HasValue && (substitute == staffId || !trainers.Contains(substitute.Value)))
                throw new Exception("El reemplazo debe ser otro entrenador activo.");
            if (!scope.IsAdmin && (staffId == userId || substitute == userId)) throw new Exception(OwnSessionMessage);

            var now = DateTime.UtcNow;
            if (mark == null)
            {
                mark = new StaffAttendance { AcademyId = academyId, EventId = ev.Id, Date = ev.StartTime.Date, CreatedAt = now };
                _db.StaffAttendances.Add(mark);
                marks[ev.Id] = mark;
            }
            mark.StaffId = staffId;
            mark.Status = record.Status;
            mark.SubstituteStaffId = substitute;
            mark.Notes = ReceiptRecorder.Truncate(record.Notes?.Trim(), 500);
            mark.MarkedByUserId = userId;
            mark.MarkedByName = scope.Name;
            // Hora del celular si se marcó sin señal; nunca una hora futura por un reloj mal configurado.
            mark.MarkedAt = record.MarkedAt.HasValue && record.MarkedAt.Value.ToUniversalTime() < now ? record.MarkedAt.Value.ToUniversalTime() : now;
            mark.UpdatedAt = now;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<List<StaffMonthlyRowDto>> MonthlyReportAsync(Guid academyId, Guid userId, int year, int month)
    {
        var scope = await ScopeAsync(academyId, userId);
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        var now = AcademyClock.WallNow;
        var events = (await _db.Events.AsNoTracking()
                .Where(e => e.AcademyId == academyId && !e.IsDeleted && e.StartTime >= start && e.StartTime < end && e.StartTime <= now)
                .ToListAsync())
            .Where(IsSession).Where(scope.Supervises).ToList();
        var eventIds = events.Select(e => e.Id).ToList();
        var marks = await _db.StaffAttendances.AsNoTracking()
            .Where(a => a.AcademyId == academyId && a.EventId != null && eventIds.Contains(a.EventId.Value))
            .ToDictionaryAsync(a => a.EventId!.Value);

        var rows = new Dictionary<Guid, StaffMonthlyRowDto>();
        StaffMonthlyRowDto Row(Guid id)
        {
            if (!rows.TryGetValue(id, out var row)) rows[id] = row = new StaffMonthlyRowDto { StaffId = id };
            return row;
        }

        foreach (var ev in events)
        {
            var mark = marks.GetValueOrDefault(ev.Id);
            var staffId = mark?.StaffId ?? ev.TeacherId;
            if (staffId == null) continue;
            var row = Row(staffId.Value);
            row.Programmed++;
            if (mark == null) { row.Unmarked++; continue; }
            switch (mark.Status)
            {
                case AttendanceStatus.Present: row.Dictated++; break;
                case AttendanceStatus.Late: row.Dictated++; row.Late++; break;
                case AttendanceStatus.Absent: row.Absent++; break;
                case AttendanceStatus.Excused: row.Excused++; break;
            }
            if (mark.SubstituteStaffId is Guid substitute)
            {
                var replacement = Row(substitute);
                replacement.Dictated++;
                replacement.Substitutions++;
            }
        }

        var names = await NamesAsync(academyId, rows.Keys);
        foreach (var row in rows.Values) row.Name = names.GetValueOrDefault(row.StaffId, "Entrenador");
        return rows.Values.OrderBy(r => r.Name).ToList();
    }
}
