using System.Text.Json;
using HSAcademia.Application.DTOs.Configuration;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>Consulta de la bitácora de la academia, con las acciones en lenguaje claro.</summary>
public class ActivityService
{
    private const int MaxPageSize = 100;

    private static readonly Dictionary<string, string[]> ModulePrefixes = new()
    {
        ["accesos"] = new[] { "auth.", "users.", "roles." },
        ["finanzas"] = new[] { "finances." },
        ["deportivo"] = new[] { "attendance.", "students.", "calendar." },
        ["tienda"] = new[] { "store." }
    };

    private static readonly Dictionary<string, string> Labels = new()
    {
        ["auth.login"] = "Inició sesión",
        ["auth.login_failed"] = "Intento de ingreso fallido",
        ["auth.password.changed"] = "Cambió su contraseña",
        ["users.created"] = "Creó una cuenta",
        ["users.updated"] = "Modificó una cuenta",
        ["users.password.reset"] = "Generó una contraseña temporal",
        ["users.ownership.transferred"] = "Transfirió la propiedad de la academia",
        ["users.profile.updated"] = "Actualizó su perfil",
        ["users.employment.updated"] = "Modificó una ficha laboral",
        ["roles.created"] = "Creó un rol",
        ["roles.updated"] = "Modificó un rol",
        ["roles.activated"] = "Activó un rol",
        ["roles.deactivated"] = "Desactivó un rol",
        ["roles.deleted"] = "Eliminó un rol",
        ["attendance.reopen"] = "Reabrió una lista de asistencia",
        ["students.evaluation.create"] = "Registró una evaluación",
        ["students.evaluation.delete"] = "Eliminó una evaluación",
        ["calendar.series.create"] = "Creó una serie de entrenamientos",
        ["calendar.series.update"] = "Modificó una serie de entrenamientos",
        ["calendar.series.delete"] = "Eliminó una serie de entrenamientos",
        ["calendar.match.result"] = "Registró un resultado",
        ["finances.config.update"] = "Modificó la configuración de cobros",
        ["finances.debts.generate"] = "Generó mensualidades",
        ["finances.payment.register"] = "Registró un pago",
        ["finances.payment.recalculate"] = "Recalculó una deuda",
        ["finances.payment.exclude"] = "Exoneró una deuda",
        ["finances.debt.regularize"] = "Decidió la mora de una deuda",
        ["finances.proof.submit"] = "Envió un comprobante",
        ["finances.proof.approve"] = "Aprobó un comprobante",
        ["finances.proof.reject"] = "Rechazó un comprobante",
        ["finances.receipt.create"] = "Emitió un recibo",
        ["finances.receipt.void"] = "Anuló un recibo",
        ["finances.cash.declare"] = "Rindió su caja",
        ["finances.cash.receive"] = "Recibió una rendición de caja",
        ["finances.discount.update"] = "Modificó un descuento",
        ["finances.discount.delete"] = "Eliminó un descuento",
        ["finances.expense.update"] = "Modificó un gasto",
        ["finances.expense.delete"] = "Eliminó un gasto",
        ["finances.pettycash.transaction"] = "Registró un movimiento de caja chica",
        ["finances.staffpayment.paid"] = "Pagó una nómina",
        ["finances.period.close"] = "Cerró un mes contable",
        ["store.product.stock_adjust"] = "Ajustó el stock de un producto",
        ["store.sale.create"] = "Registró una venta",
        ["store.sale.void"] = "Anuló una venta"
    };

    private readonly AppDbContext _db;

    public ActivityService(AppDbContext db) => _db = db;

    public async Task<ActivityPageDto> GetAsync(Guid academyId, string? module, DateTime? from, DateTime? to, string? search, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.AuditLogs.AsNoTracking().Where(a => a.AcademyId == academyId);

        if (!string.IsNullOrWhiteSpace(module) && ModulePrefixes.TryGetValue(module, out var prefixes))
        {
            var first = prefixes[0];
            var second = prefixes.ElementAtOrDefault(1) ?? first;
            var third = prefixes.ElementAtOrDefault(2) ?? first;
            query = query.Where(a => a.Action.StartsWith(first) || a.Action.StartsWith(second) || a.Action.StartsWith(third));
        }

        if (from.HasValue) { var start = LocalDayStartUtc(from.Value); query = query.Where(a => a.CreatedAt >= start); }
        if (to.HasValue) { var end = LocalDayStartUtc(to.Value.AddDays(1)); query = query.Where(a => a.CreatedAt < end); }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            var people = await _db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(u => u.AcademyId == academyId && EF.Functions.ILike(u.FirstName + " " + u.LastName, term))
                .Select(u => u.Id)
                .ToListAsync();
            var targets = people.Select(id => id.ToString()).ToList();
            query = query.Where(a => (a.UserId != null && people.Contains(a.UserId.Value)) || (a.EntityId != null && targets.Contains(a.EntityId)));
        }

        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(a => a.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var ids = rows.Where(r => r.UserId != null).Select(r => r.UserId!.Value)
            .Concat(rows.Where(r => r.EntityType == "User" && r.EntityId != null)
                .Select(r => Guid.TryParse(r.EntityId, out var id) ? id : Guid.Empty))
            .Where(id => id != Guid.Empty).Distinct().ToList();
        var names = await _db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

        return new ActivityPageDto
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(r => new ActivityItemDto
            {
                Id = r.Id,
                CreatedAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc),
                Action = r.Action,
                Description = Labels.GetValueOrDefault(r.Action, r.Action),
                Module = ModuleOf(r.Action),
                ActorName = r.UserId != null ? names.GetValueOrDefault(r.UserId.Value, "Cuenta eliminada") : "Sistema",
                TargetName = r.EntityType == "User" && Guid.TryParse(r.EntityId, out var target) && target != r.UserId
                    ? names.GetValueOrDefault(target) : null,
                IpAddress = r.IpAddress,
                Device = DeviceOf(r.UserAgent),
                Details = Parse(r.NewValues)
            }).ToList()
        };
    }

    /// <summary>Nombre corto del dispositivo a partir del agente de usuario.</summary>
    public static string DeviceOf(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Desconocido";
        return userAgent.Contains("okhttp", StringComparison.OrdinalIgnoreCase) ? "App móvil" : "Navegador web";
    }

    private static string ModuleOf(string action) =>
        ModulePrefixes.FirstOrDefault(m => m.Value.Any(action.StartsWith)).Key ?? "otros";

    private static DateTime LocalDayStartUtc(DateTime localDate) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified), AcademyClock.Zone);

    private static JsonElement? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonDocument.Parse(json).RootElement.Clone(); }
        catch (JsonException) { return null; }
    }
}
