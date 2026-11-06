using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Fuente ÚNICA para saber si un mes contable está cerrado.
/// Antes existían dos mecanismos (MonthlyClosings y FinancialPeriods) y cada módulo revisaba uno distinto.
/// Regla: manda FinancialPeriods (permite cerrar y reabrir). Por compatibilidad, un MonthlyClosing cerrado
/// sin registro en FinancialPeriods también cuenta como cerrado; al reabrir se crea el registro.
/// </summary>
public class FinancialPeriodGuard
{
    private readonly AppDbContext _db;

    public FinancialPeriodGuard(AppDbContext db) => _db = db;

    public async Task<bool> IsClosedAsync(Guid academyId, int year, int month)
    {
        var period = await _db.FinancialPeriods.AsNoTracking()
            .Where(p => p.AcademyId == academyId && p.Year == year && p.Month == month)
            .Select(p => (bool?)p.IsClosed)
            .FirstOrDefaultAsync();

        if (period.HasValue) return period.Value;

        return await _db.MonthlyClosings.AsNoTracking().AnyAsync(c =>
            c.AcademyId == academyId && c.Year == year && c.Month == month &&
            c.Status == MonthlyClosingStatus.Closed);
    }

    /// <summary>Lanza una excepción con mensaje para el usuario si el mes está cerrado.</summary>
    public async Task EnsureOpenAsync(Guid academyId, int year, int month, string action)
    {
        if (await IsClosedAsync(academyId, year, month))
        {
            var name = new DateTime(year, month, 1).ToString("MMMM yyyy", Culture.Es);
            throw new Exception($"No se puede {action}: el periodo contable de {name} está cerrado.");
        }
    }

    public Task EnsureOpenAsync(Guid academyId, DateTime localDate, string action) =>
        EnsureOpenAsync(academyId, localDate.Year, localDate.Month, action);
}

public static class Culture
{
    public static readonly System.Globalization.CultureInfo Es = new("es-PE");

    /// <summary>Descripción estándar de una mensualidad, independiente del idioma del servidor.</summary>
    public static string MonthlyFeeDescription(int year, int month) =>
        $"Mensualidad {new DateTime(year, month, 1).ToString("MMMM yyyy", Es)}";
}
