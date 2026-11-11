using HSAcademia.API.Authorization;
using HSAcademia.Infrastructure.Persistence;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.API.Controllers;

/// <summary>Reportes descargables en CSV (se abren directamente en Excel).</summary>
[ApiController]
[Route("api/reports")]
[Authorize(Roles = "AcademyAdmin,Staff")]
public class ReportsController : ControllerBase
{
    private readonly ReportsService _reports;

    public ReportsController(ReportsService reports) => _reports = reports;

    private FileContentResult Csv(byte[] content, string name) =>
        File(content, "text/csv; charset=utf-8", $"{name}_{AcademyClock.Today:yyyyMMdd}.csv");

    private static void ValidatePeriod(int year, int month)
    {
        if (year < 2000 || year > 2100 || month < 1 || month > 12) throw new Exception("Periodo inválido.");
    }

    [HttpGet("overdue")]
    [RequirePermission(Permissions.ReportsFinance)]
    public async Task<IActionResult> Overdue() =>
        Csv(await _reports.OverdueAgingCsvAsync(User.GetAcademyId()), "morosidad");

    [HttpGet("payments")]
    [RequirePermission(Permissions.ReportsFinance)]
    public async Task<IActionResult> Payments([FromQuery] int year, [FromQuery] int month)
    {
        ValidatePeriod(year, month);
        return Csv(await _reports.PaymentsCsvAsync(User.GetAcademyId(), year, month), $"cobros_{year}-{month:00}");
    }

    [HttpGet("students")]
    [RequirePermission(Permissions.ReportsStudents)]
    public async Task<IActionResult> Students([FromQuery] bool includeInactive = false) =>
        Csv(await _reports.StudentsCsvAsync(User.GetAcademyId(), includeInactive && User.IsInRole("AcademyAdmin")), "padron_alumnos");

    [HttpGet("attendance")]
    [RequirePermission(Permissions.ReportsAttendance)]
    public async Task<IActionResult> Attendance([FromQuery] int year, [FromQuery] int month, [FromServices] AppDbContext db)
    {
        ValidatePeriod(year, month);
        List<Guid>? scope = null;
        if (!User.IsInRole("AcademyAdmin"))
        {
            var userId = User.GetUserId();
            scope = await db.Users.Where(u => u.Id == userId).SelectMany(u => u.AssignedCategories.Select(c => c.Id)).ToListAsync();
        }
        return Csv(await _reports.AttendanceCsvAsync(User.GetAcademyId(), year, month, scope), $"asistencia_{year}-{month:00}");
    }
}
