using System.Security.Claims;
using System.Text.Json;
using HSAcademia.Domain.Entities;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Registro de auditoría de operaciones sensibles (cobros, exoneraciones, anulaciones, cierres, reseteos).
/// Agrega la entrada al DbContext: se guarda en el MISMO SaveChanges de la operación,
/// así nunca queda una acción sin su registro (ni un registro de una acción que falló).
/// </summary>
public class AuditService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public void Add(Guid? academyId, string action, string entityType, Guid? entityId, object? data = null)
    {
        var ctx = _http.HttpContext;
        var userIdValue = ctx?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx?.User.FindFirstValue("sub");

        _db.AuditLogs.Add(new AuditLog
        {
            AcademyId = academyId,
            UserId = Guid.TryParse(userIdValue, out var uid) ? uid : null,
            Action = action,
            EntityType = entityType,
            EntityId = entityId?.ToString(),
            NewValues = data == null ? null : JsonSerializer.Serialize(data, JsonOpts),
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Truncate(ctx?.Request.Headers.UserAgent.ToString(), 500),
            CreatedAt = DateTime.UtcNow
        });
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= max ? value : value[..max];
}
