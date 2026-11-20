using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Avisos programados (los envía el despachador de notificaciones):
/// <list type="bullet">
/// <item>Encuentros (amistosos y de torneo) con hora: X horas antes, a las familias de los convocados y al responsable.</item>
/// <item>Encuentros sin hora confirmada: ese día desde la hora configurada ("consulta el horario con el coordinador").</item>
/// <item>Cumpleaños: saludo de la academia a la familia; aviso al personal (entrenadores de su categoría,
/// coordinadores y administradores). Los compañeros no reciben push: lo ven en el Inicio de la app.</item>
/// </list>
/// Cada aviso tiene una clave única: correr esto cada pocos minutos nunca lo repite.
/// </summary>
public class ScheduledNoticeService
{
    /// <summary>No se envían saludos ni avisos "sin hora" de noche.</summary>
    private const int LastHour = 21;
    public const string DefaultBirthdayMessage =
        "¡Feliz cumpleaños, {nombre}! 🎉 Toda la familia de {academia} te desea un día lleno de alegría. " +
        "Gracias por tu esfuerzo y tu entusiasmo en cada entrenamiento: ¡que este nuevo año venga con muchos goles, aprendizajes y amigos! ⚽";

    private readonly AppDbContext _db;
    public ScheduledNoticeService(AppDbContext db) => _db = db;

    /// <param name="localNow">Hora local de la academia (Lima).</param>
    public async Task<int> CreateDueAsync(DateTime localNow, CancellationToken ct = default)
    {
        var academies = await _db.Academies.AsNoTracking().Where(a => !a.IsDeleted && a.Status == AcademyStatus.Active).ToListAsync(ct);
        var created = 0;
        foreach (var a in academies)
        {
            if (a.NotifyMatchEnabled) created += await MatchRemindersAsync(a, localNow, ct);
            if (a.NotifyBirthdayEnabled || a.NotifyBirthdayStaff) created += await BirthdaysAsync(a, localNow, ct);
        }
        return created;
    }

    // ═════════════════════════ Encuentros ═════════════════════════
    private async Task<int> MatchRemindersAsync(Academy a, DateTime localNow, CancellationToken ct)
    {
        var wallNow = DateTime.SpecifyKind(localNow, DateTimeKind.Utc); // convención de Event.StartTime
        var hours = Math.Clamp(a.NotifyMatchHoursBefore, 1, 24);
        var dayStart = wallNow.Date;
        var dayEnd = dayStart.AddDays(1);
        var events = await _db.Events.AsNoTracking()
            .Include(e => e.Tournament)
            .Where(e => e.AcademyId == a.AcademyId && !e.IsDeleted && e.IsActive
                     && (e.Type == EventType.FriendlyMatch || e.Type == EventType.TournamentMatch)
                     && (e.MatchStatus == null || e.MatchStatus == MatchStatus.Scheduled)
                     && ((!e.TimePending && e.StartTime > wallNow && e.StartTime <= wallNow.AddHours(hours))
                         || (e.TimePending && e.StartTime >= dayStart && e.StartTime < dayEnd)))
            .ToListAsync(ct);
        var noTimeHour = Math.Clamp(a.NotifyMatchNoTimeHour, 5, 20);
        var created = 0;
        foreach (var ev in events)
        {
            if (ev.TimePending && (localNow.Hour < noTimeHour || localNow.Hour >= LastHour)) continue;
            var students = await PlayersAsync(ev, ct);
            var what = Describe(ev);
            foreach (var s in students)
            {
                var key = ev.TimePending ? $"mday:{ev.Id:N}:{s.Id:N}" : $"match:{ev.Id:N}:{s.Id:N}";
                var (title, body) = ev.TimePending
                    ? ($"Hoy {s.FirstName} tiene encuentro",
                       $"Hoy hay {what}. El horario aún no está confirmado: consúltalo con el coordinador de la academia. ¡Que tenga un gran partido!")
                    : ($"⚽ Hoy juega {s.FirstName} · {ev.StartTime:HH:mm}",
                       $"{Capitalize(what)} a las {ev.StartTime:HH:mm}{Place(ev)}.{Call(ev)} Lleva tu uniforme e hidratación. ¡Vamos con todo!");
                if (await AddFamilyAsync(a.AcademyId, s.Id, ev.Id, FamilyNoticeType.MatchReminder, key, title, body, ct)) created++;
            }
            if (ev.TeacherId is Guid teacher)
            {
                var key = ev.TimePending ? $"smday:{ev.Id:N}:{teacher:N}" : $"smatch:{ev.Id:N}:{teacher:N}";
                var (title, body) = ev.TimePending
                    ? ("Encuentro de hoy sin horario", $"Hoy hay {what} y aún no tiene hora confirmada. Complétala en el Calendario para avisar a las familias.")
                    : ($"Recordatorio: encuentro a las {ev.StartTime:HH:mm}", $"{Capitalize(what)}{Place(ev)}.{Call(ev)} Convocados: {students.Count}.");
                if (await AddStaffAsync(a.AcademyId, teacher, ev.Id, StaffNoticeType.MatchReminder, key, title, body, ct)) created++;
            }
        }
        return created;
    }

    /// <summary>Convocados (sin los que dijeron que no van); si no hay convocatoria, los alumnos activos de sus categorías.</summary>
    private async Task<List<Student>> PlayersAsync(Event ev, CancellationToken ct)
    {
        var called = await _db.EventCalls.AsNoTracking().Where(c => c.EventId == ev.Id && c.IsConfirmed != false)
            .Select(c => c.StudentId).ToListAsync(ct);
        var hasCalls = await _db.EventCalls.AsNoTracking().AnyAsync(c => c.EventId == ev.Id, ct);
        if (hasCalls)
            return await _db.Students.AsNoTracking().Where(s => called.Contains(s.Id) && s.IsActive && !s.IsDeleted).ToListAsync(ct);
        var cats = ev.AllCategoryIds();
        if (cats.Count == 0) return new();
        return await _db.Students.AsNoTracking()
            .Where(s => s.AcademyId == ev.AcademyId && cats.Contains(s.CategoryId) && s.IsActive && !s.IsDeleted).ToListAsync(ct);
    }

    private static string Describe(Event ev)
    {
        var kind = ev.Type == EventType.TournamentMatch && ev.Tournament != null ? $"un encuentro del torneo {ev.Tournament.Name}" : "un partido amistoso";
        var round = string.IsNullOrWhiteSpace(ev.Round) ? "" : $" ({ev.Round})";
        var rival = string.IsNullOrWhiteSpace(ev.OpponentTeam) ? "" : $" vs {ev.OpponentTeam}";
        return $"{kind}{round}{rival}";
    }
    private static string Place(Event ev) => string.IsNullOrWhiteSpace(ev.Location) ? "" : $" en {ev.Location}";
    private static string Call(Event ev) => ev.CallTime is DateTime c ? $" Citación: {c:HH:mm}." : "";
    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    // ═════════════════════════ Cumpleaños ═════════════════════════
    private async Task<int> BirthdaysAsync(Academy a, DateTime localNow, CancellationToken ct)
    {
        var hour = Math.Clamp(a.NotifyBirthdayHour, 5, 20);
        if (localNow.Hour < hour || localNow.Hour >= LastHour) return 0;
        var today = localNow.Date;
        var kids = (await _db.Students.AsNoTracking().Include(s => s.Category)
                .Where(s => s.AcademyId == a.AcademyId && s.IsActive && !s.IsDeleted && s.DateOfBirth != null)
                .ToListAsync(ct))
            .Where(s => IsBirthday(s.DateOfBirth!.Value, today))
            .ToList();
        if (kids.Count == 0) return 0;
        var created = 0;

        if (a.NotifyBirthdayEnabled)
        {
            var template = string.IsNullOrWhiteSpace(a.BirthdayMessage) ? DefaultBirthdayMessage : a.BirthdayMessage!;
            foreach (var s in kids)
            {
                var body = template.Replace("{nombre}", s.FirstName).Replace("{academia}", a.Name)
                    .Replace("{edad}", AgeOn(s.DateOfBirth!.Value, today).ToString());
                if (await AddFamilyAsync(a.AcademyId, s.Id, null, FamilyNoticeType.Birthday, $"bday:{today:yyyyMMdd}:{s.Id:N}",
                        $"🎉 ¡Feliz cumpleaños, {s.FirstName}!", body, ct)) created++;
            }
        }

        if (a.NotifyBirthdayStaff)
        {
            // Destinatarios: administradores, coordinadores (de esas categorías o de toda la academia) y entrenadores de la categoría.
            var staff = await _db.Users.AsNoTracking()
                .Include(u => u.AssignedRoles).Include(u => u.AssignedCategories)
                .Where(u => u.AcademyId == a.AcademyId && !u.IsDeleted && u.Status == UserStatus.Active
                         && (u.Role == UserRole.AcademyAdmin || u.Role == UserRole.Staff))
                .ToListAsync(ct);
            foreach (var u in staff)
            {
                var cats = u.AssignedCategories.Select(c => c.Id).ToHashSet();
                var coordinator = u.AssignedRoles.Any(r => r.SystemKey == "coordinator" || r.Name.Contains("oordinador", StringComparison.OrdinalIgnoreCase));
                var mine = kids.Where(s => u.Role == UserRole.AcademyAdmin
                                         || (coordinator && (cats.Count == 0 || cats.Contains(s.CategoryId)))
                                         || cats.Contains(s.CategoryId)).ToList();
                if (mine.Count == 0) continue;
                var title = mine.Count == 1 ? $"🎂 Hoy cumple {mine[0].FirstName} {mine[0].LastName}" : $"🎂 Hoy cumplen {mine.Count} alumnos";
                var list = string.Join(", ", mine.Take(6).Select(s => $"{s.FirstName} {s.LastName} ({s.Category?.Name}, {AgeOn(s.DateOfBirth!.Value, today)} años)"));
                if (mine.Count > 6) list += $" y {mine.Count - 6} más";
                var body = $"{list}. ¡Salúdalos en el entrenamiento!";
                if (await AddStaffAsync(a.AcademyId, u.Id, null, StaffNoticeType.Birthday, $"sbday:{today:yyyyMMdd}:{u.Id:N}", title, body, ct)) created++;
            }
        }
        return created;
    }

    /// <summary>¿Cumple hoy? Quien nació un 29 de febrero lo celebra el 28 en años no bisiestos.</summary>
    public static bool IsBirthday(DateTime dob, DateTime today) =>
        dob.Month == today.Month && (dob.Day == today.Day
            || (dob.Month == 2 && dob.Day == 29 && today.Day == 28 && !DateTime.IsLeapYear(today.Year)));

    public static int AgeOn(DateTime dob, DateTime day)
    {
        var age = day.Year - dob.Year;
        if (day.Month < dob.Month || (day.Month == dob.Month && day.Day < dob.Day)) age--;
        return age;
    }

    // ═════════════════════════ Guardado sin duplicar ═════════════════════════
    private async Task<bool> AddFamilyAsync(Guid academyId, Guid studentId, Guid? eventId, FamilyNoticeType type, string key, string title, string body, CancellationToken ct)
    {
        if (await _db.FamilyNotices.AnyAsync(n => n.DedupeKey == key, ct)) return false;
        _db.FamilyNotices.Add(new FamilyNotice
        {
            AcademyId = academyId, StudentId = studentId, EventId = eventId, Type = type, DedupeKey = key,
            Title = Cut(title, 150), Body = Cut(body, 300),
        });
        return await SaveAsync(ct);
    }

    private async Task<bool> AddStaffAsync(Guid academyId, Guid userId, Guid? eventId, StaffNoticeType type, string key, string title, string body, CancellationToken ct)
    {
        if (await _db.StaffNotices.AnyAsync(n => n.DedupeKey == key, ct)) return false;
        _db.StaffNotices.Add(new StaffNotice
        {
            AcademyId = academyId, UserId = userId, EventId = eventId, Type = type, DedupeKey = key,
            Title = Cut(title, 150), Body = Cut(body, 300),
        });
        return await SaveAsync(ct);
    }

    /// <summary>Si otra instancia lo creó al mismo tiempo, el índice único lo impide y se descarta sin error.</summary>
    private async Task<bool> SaveAsync(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            return false;
        }
    }

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
