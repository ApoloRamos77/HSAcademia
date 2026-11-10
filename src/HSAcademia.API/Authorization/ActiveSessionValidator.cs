using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.API.Authorization;

/// <summary>
/// Contrasta cada token con el estado actual de la cuenta. Un token firmado solo prueba quién era el
/// usuario al iniciar sesión: una suspensión, un cambio de nivel de acceso o la baja de la academia
/// deben aplicar de inmediato y no cuando el token expire.
/// Se rechaza con 401 (y no 403) a propósito: la app y la web cierran la sesión y, al volver a ingresar,
/// el usuario recibe el motivo real; además la cola sin señal de la app conserva lo pendiente ante un 401.
/// </summary>
public static class ActiveSessionValidator
{
    private const string ChangePasswordPath = "/api/auth/change-password";

    private static readonly AcademyStatus[] BlockedAcademyStatuses =
    {
        AcademyStatus.Suspended, AcademyStatus.Inactive, AcademyStatus.Pending, AcademyStatus.Rejected
    };

    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal!;
        var userId = principal.GetUserId();
        if (userId == Guid.Empty)
        {
            context.Fail("Token sin usuario.");
            return;
        }

        var http = context.HttpContext;
        try
        {
            var db = http.RequestServices.GetRequiredService<AppDbContext>();
            var account = await db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    u.Role,
                    u.Status,
                    u.AcademyId,
                    u.MustChangePassword,
                    AcademyStatus = u.Academy != null ? (AcademyStatus?)u.Academy.Status : null
                })
                .FirstOrDefaultAsync(http.RequestAborted);

            var failure = account == null ? "La cuenta ya no existe."
                : account.Status != UserStatus.Active ? "La cuenta no está activa."
                : !principal.IsInRole(account.Role.ToString()) ? "Cambió el nivel de acceso."
                : account.AcademyId.HasValue && account.AcademyId != principal.GetAcademyId() ? "Cambió la academia."
                : account.AcademyId.HasValue && (account.AcademyStatus == null || BlockedAcademyStatuses.Contains(account.AcademyStatus.Value)) ? "La academia no está activa."
                : account.MustChangePassword && !http.Request.Path.StartsWithSegments(ChangePasswordPath) ? "Debe cambiar la contraseña."
                : null;

            if (failure != null) context.Fail(failure);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Una caída momentánea de la base no debe cerrar la sesión de todos: sin base ningún endpoint
            // protegido puede operar, así que la petición terminará en error 500 y el cliente reintentará.
            http.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(nameof(ActiveSessionValidator))
                .LogError(ex, "No se pudo validar el estado de la cuenta {UserId}", userId);
        }
    }
}
