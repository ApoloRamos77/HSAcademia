using HSAcademia.Application.DTOs.Dashboard;
using HSAcademia.Application.Interfaces;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Indicadores del panel principal. El personal (Staff) solo ve los alumnos/eventos de sus categorías
/// y el bloque financiero solo se incluye si el usuario puede ver finanzas.
/// </summary>
public class DashboardService
{
    private const int AbsenceStreakAlert = 3;
    private const int MaxAbsenceStreaks = 10;

    private readonly AppDbContext _db;
    private readonly IFinancesPremiumService _finances;

    public DashboardService(AppDbContext db, IFinancesPremiumService finances)
    {
        _db = db;
        _finances = finances;
    }

    public async Task<AcademyDashboardDto> GetAcademyDashboardAsync(
        Guid academyId, Guid userId, bool isAdmin, bool canSeeFinances)
    {
        var now = AcademyClock.Now;
        var today = AcademyClock.Today;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var (monthStartUtc, monthEndUtc) = AcademyClock.MonthRangeUtc(now.Year, now.Month);

        // ── Alcance: categorías visibles ────────────────────────────
        List<Guid>? scope = null;
        if (!isAdmin)
        {
            scope = await _db.Users.Where(u => u.Id == userId && u.AcademyId == academyId)
                .SelectMany(u => u.AssignedCategories.Select(c => c.Id))
                .ToListAsync();
        }

        var studentsQuery = _db.Students.AsNoTracking().Where(s => s.AcademyId == academyId);
        if (scope != null) studentsQuery = studentsQuery.Where(s => scope.Contains(s.CategoryId));

        var students = await studentsQuery
            .Select(s => new
            {
                s.Id, s.FirstName, s.LastName, s.IsActive, s.EnrollmentDate, s.WithdrawalDate,
                s.DateOfBirth, s.CategoryId, s.GuardianId, s.Phone, s.IsScholarship, s.ScholarshipPercentage,
                Category = s.Category.Name, Headquarter = s.Headquarter.Name,
                GuardianPhone = s.Guardian != null ? s.Guardian.Phone : null,
                HasEmergencyContact = s.MedicalRecord != null && s.MedicalRecord.EmergencyContactPhone != null
            })
            .ToListAsync();

        var active = students.Where(s => s.IsActive).ToList();
        var dto = new AcademyDashboardDto { Year = now.Year, Month = now.Month };

        dto.Students = new StudentsKpiDto
        {
            Active = active.Count,
            NewThisMonth = active.Count(s => s.EnrollmentDate >= monthStartUtc && s.EnrollmentDate < monthEndUtc),
            WithdrawnThisMonth = students.Count(s => s.WithdrawalDate.HasValue &&
                s.WithdrawalDate.Value >= monthStartUtc && s.WithdrawalDate.Value < monthEndUtc),
            Scholarship = active.Count(s => s.IsScholarship || (s.ScholarshipPercentage ?? 0) > 0),
            ByCategory = active
                .GroupBy(s => new { s.CategoryId, s.Category, s.Headquarter })
                .Select(g => new CategoryCountDto { CategoryId = g.Key.CategoryId, Category = g.Key.Category, Headquarter = g.Key.Headquarter, Count = g.Count() })
                .OrderByDescending(c => c.Count)
                .ToList()
        };

        // ── Finanzas ────────────────────────────────────────────────
        if (canSeeFinances)
            dto.Finance = await BuildFinanceAsync(academyId, now, today);

        // ── Asistencia (últimos 30 días) ───────────────────────────
        var from = today.AddDays(-30);
        var visibleIds = students.Select(s => s.Id).ToList();
        var attendance = await _db.Attendances.AsNoTracking()
            .Where(a => a.AcademyId == academyId && a.Date >= from && a.Date <= today && visibleIds.Contains(a.StudentId))
            .Select(a => new { a.StudentId, a.Status, a.Date, a.CreatedAt })
            .ToListAsync();

        static bool Attended(AttendanceStatus s) => s == AttendanceStatus.Present || s == AttendanceStatus.Late;
        var countable = attendance.Where(a => a.Status != AttendanceStatus.Excused).ToList();
        dto.Attendance = new AttendanceKpiDto
        {
            RecordsLast30Days = attendance.Count,
            RateLast30Days = countable.Count == 0 ? null
                : Math.Round(100m * countable.Count(a => Attended(a.Status)) / countable.Count, 1),
            LowAttendance = countable
                .GroupBy(a => a.StudentId)
                .Where(g => g.Count() >= 4)
                .Select(g => new { g.Key, Rate = Math.Round(100m * g.Count(a => Attended(a.Status)) / g.Count(), 1), Records = g.Count() })
                .Where(x => x.Rate < 70)
                .OrderBy(x => x.Rate)
                .Take(5)
                .Select(x =>
                {
                    var s = students.First(st => st.Id == x.Key);
                    return new LowAttendanceStudentDto { StudentId = s.Id, Name = $"{s.FirstName} {s.LastName}", Category = s.Category, Rate = x.Rate, Records = x.Records };
                })
                .ToList()
        };

        // Las justificadas ni suman a la racha ni la cortan.
        var activeById = active.ToDictionary(s => s.Id);
        dto.Attendance.AbsenceStreaks = countable
            .Where(a => activeById.ContainsKey(a.StudentId))
            .GroupBy(a => a.StudentId)
            .Select(g =>
            {
                var streak = g.OrderByDescending(a => a.Date).ThenByDescending(a => a.CreatedAt)
                    .TakeWhile(a => a.Status == AttendanceStatus.Absent).ToList();
                return new { StudentId = g.Key, Streak = streak };
            })
            .Where(x => x.Streak.Count >= AbsenceStreakAlert)
            .OrderByDescending(x => x.Streak.Count)
            .Take(MaxAbsenceStreaks)
            .Select(x =>
            {
                var s = activeById[x.StudentId];
                return new AbsenceStreakDto
                {
                    StudentId = s.Id,
                    Name = $"{s.FirstName} {s.LastName}",
                    Category = s.Category,
                    Absences = x.Streak.Count,
                    AbsentSince = x.Streak[^1].Date,
                    ContactPhone = s.GuardianPhone ?? s.Phone
                };
            })
            .ToList();

        // ── Próximos eventos ───────────────────────────────────────
        var categoryNames = await _db.Categories.AsNoTracking().Where(c => c.AcademyId == academyId)
            .ToDictionaryAsync(c => c.Id, c => c.Name);
        var upcoming = await _db.Events.AsNoTracking()
            .Where(e => e.AcademyId == academyId && e.IsActive && e.StartTime >= AcademyClock.WallNow)
            .OrderBy(e => e.StartTime)
            .Take(30)
            .ToListAsync();
        dto.UpcomingEvents = upcoming
            .Select(e => new { e, Cats = EventCategories(e) })
            .Where(x => scope == null || x.e.TeacherId == userId || x.Cats.Count == 0 || x.Cats.Intersect(scope).Any())
            .Take(5)
            .Select(x => new DashboardEventDto
            {
                Id = x.e.Id,
                Title = x.e.Title,
                Type = (int)x.e.Type,
                StartTime = x.e.StartTime,
                Opponent = x.e.OpponentTeam,
                Categories = x.Cats.Count == 0 ? "Todas" :
                    string.Join(", ", x.Cats.Select(id => categoryNames.GetValueOrDefault(id)).Where(n => n != null))
            })
            .ToList();

        // ── Cumpleaños (próximos 7 días) ───────────────────────────
        dto.Birthdays = active
            .Where(s => s.DateOfBirth.HasValue)
            .Select(s =>
            {
                var dob = s.DateOfBirth!.Value;
                var next = SafeDate(today.Year, dob.Month, dob.Day);
                if (next < today) next = SafeDate(today.Year + 1, dob.Month, dob.Day);
                return new { s, next, age = next.Year - dob.Year };
            })
            .Where(x => (x.next - today).TotalDays <= 7)
            .OrderBy(x => x.next)
            .Select(x => new DashboardBirthdayDto
            {
                StudentId = x.s.Id,
                Name = $"{x.s.FirstName} {x.s.LastName}",
                Category = x.s.Category,
                Date = x.next,
                TurnsAge = x.age,
                IsToday = x.next == today
            })
            .ToList();

        // ── Alertas operativas ─────────────────────────────────────
        if (dto.Finance is { OverdueStudents: > 0 })
            dto.Alerts.Add(new DashboardAlertDto { Level = "danger", Message = $"{dto.Finance.OverdueStudents} alumno(s) con mensualidades vencidas (S/ {dto.Finance.OverdueAmount:N2}).", Link = "/academy/finanzas" });

        if (isAdmin)
        {
            var noFee = await _db.Categories.CountAsync(c => c.AcademyId == academyId && c.IsActive && c.MonthlyFee <= 0);
            if (noFee > 0)
                dto.Alerts.Add(new DashboardAlertDto { Level = "warning", Message = $"{noFee} categoría(s) sin mensualidad configurada: no se generarán cobros para sus alumnos.", Link = "/academy/categorias" });

            var debtsThisMonth = await _db.PaymentRecords.AnyAsync(p => p.AcademyId == academyId &&
                p.Type == PaymentType.MonthlyFee && p.PeriodYear == now.Year && p.PeriodMonth == now.Month);
            if (!debtsThisMonth && active.Count > 0)
                dto.Alerts.Add(new DashboardAlertDto { Level = "warning", Message = "Aún no se generaron las mensualidades de este mes.", Link = "/academy/finanzas" });
        }

        var noContact = active.Count(s => s.GuardianId == null && string.IsNullOrWhiteSpace(s.Phone));
        if (noContact > 0)
            dto.Alerts.Add(new DashboardAlertDto { Level = "warning", Message = $"{noContact} alumno(s) sin apoderado ni teléfono de contacto.", Link = "/academy/alumnos" });

        var noEmergency = active.Count(s => !s.HasEmergencyContact);
        if (noEmergency > 0)
            dto.Alerts.Add(new DashboardAlertDto { Level = "info", Message = $"{noEmergency} alumno(s) sin contacto de emergencia en la ficha médica.", Link = "/academy/alumnos" });

        if (dto.Attendance.AbsenceStreaks.Count > 0)
            dto.Alerts.Add(new DashboardAlertDto { Level = "warning", Message = $"{dto.Attendance.AbsenceStreaks.Count} alumno(s) con {AbsenceStreakAlert} o más faltas seguidas: conviene llamar a la familia.", Link = "/academy/asistencia-metricas" });

        if (dto.Attendance.LowAttendance.Count > 0)
            dto.Alerts.Add(new DashboardAlertDto { Level = "info", Message = $"{dto.Attendance.LowAttendance.Count} alumno(s) con asistencia menor al 70% en los últimos 30 días.", Link = "/academy/asistencia-metricas" });

        return dto;
    }

    private async Task<FinanceKpiDto> BuildFinanceAsync(Guid academyId, DateTime now, DateTime today)
    {
        var summary = await _finances.GetFinanceSummaryAsync(academyId, now.Month, now.Year);
        var trend = await _finances.GetTrendDataAsync(academyId, 6);

        var todayUtc = DateTime.SpecifyKind(today, DateTimeKind.Utc);
        var overdue = await _db.PaymentRecords.AsNoTracking()
            .Where(p => p.AcademyId == academyId && !p.IsPaid && p.Type != PaymentType.ProductSale && p.DueDate < todayUtc)
            .Select(p => new { p.StudentId, Pending = p.Amount - p.AmountPaid, p.DueDate, Name = p.Student.FirstName + " " + p.Student.LastName, Category = p.Student.Category.Name })
            .ToListAsync();

        return new FinanceKpiDto
        {
            Billed = summary.TotalBilled,
            Collected = summary.TotalIncome,
            Pending = summary.TotalPending,
            CollectionRate = summary.TotalBilled > 0
                ? Math.Round(100m * (summary.TotalBilled - summary.TotalPending) / summary.TotalBilled, 1) : 0,
            OverdueAmount = overdue.Sum(o => o.Pending),
            OverdueStudents = overdue.Select(o => o.StudentId).Distinct().Count(),
            StoreRevenue = summary.TotalStoreRevenue,
            Expenses = summary.TotalExpenses + summary.TotalStaffPayments,
            NetBalance = summary.NetBalance,
            Trend = trend.Select(t => new FinanceTrendPointDto
            {
                Label = t.Label,
                Collected = t.Income + t.StoreRevenue,
                Expenses = t.Expenses + t.StaffPayments,
                NetBalance = t.NetBalance
            }).ToList(),
            TopOverdue = overdue
                .GroupBy(o => new { o.StudentId, o.Name, o.Category })
                .Select(g => new OverdueStudentDto
                {
                    StudentId = g.Key.StudentId,
                    Name = g.Key.Name,
                    Category = g.Key.Category,
                    Amount = g.Sum(x => x.Pending),
                    MaxDaysOverdue = g.Max(x => (today - x.DueDate.Date).Days)
                })
                .OrderByDescending(x => x.Amount)
                .Take(5)
                .ToList()
        };
    }

    private static List<Guid> EventCategories(Event e)
    {
        var list = new List<Guid>();
        if (e.CategoryId.HasValue) list.Add(e.CategoryId.Value);
        if (e.CategoryIds != null) list.AddRange(e.CategoryIds);
        return list.Distinct().ToList();
    }

    private static DateTime SafeDate(int year, int month, int day) =>
        new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
}
