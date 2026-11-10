using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HSAcademia.API.Authorization;

/// <summary>
/// Exige que el usuario tenga al menos UNO de los permisos indicados.
/// - AcademyAdmin y SuperAdmin pasan siempre.
/// - Staff: se suman los permisos de sus roles activos (consultados en BD en cada request,
///   así un cambio aplica de inmediato sin esperar a que expire el token).
/// - Alumno / Apoderado: denegado.
/// AcademyWide: la función no se puede acotar a una sede (contabilidad general), así que exige
/// no tener sedes asignadas. Debe usarse junto con [Authorize]; este filtro solo evalúa permisos.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _anyOf;

    public RequirePermissionAttribute(params string[] anyOf) => _anyOf = anyOf;

    public bool AcademyWide { get; set; }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (user.IsInRole("AcademyAdmin") || user.IsInRole("SuperAdmin"))
            return;

        if (!user.IsInRole("Staff"))
        {
            context.Result = Forbidden("No tienes permisos para realizar esta acción.");
            return;
        }

        if (user.GetUserId() == Guid.Empty || user.GetAcademyId() == Guid.Empty)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var scope = await context.HttpContext.RequestServices.GetRequiredService<AccessScopeService>().CurrentAsync();
        if (!scope.Has(_anyOf))
            context.Result = Forbidden("No tienes permisos para realizar esta acción.");
        else if (AcademyWide && !scope.IsAcademyWide)
            context.Result = Forbidden("Esta función requiere alcance sobre toda la academia.");
    }

    private static ObjectResult Forbidden(string message) =>
        new(new { message }) { StatusCode = StatusCodes.Status403Forbidden };
}

/// <summary>Consulta puntual de permisos dentro de una acción (misma regla que [RequirePermission]).</summary>
public static class PermissionChecker
{
    public static async Task<bool> HasAsync(HttpContext http, string permission)
    {
        var user = http.User;
        if (user.IsInRole("AcademyAdmin") || user.IsInRole("SuperAdmin")) return true;
        if (!user.IsInRole("Staff")) return false;
        var scope = await http.RequestServices.GetRequiredService<AccessScopeService>().CurrentAsync();
        return scope.Has(permission);
    }
}
