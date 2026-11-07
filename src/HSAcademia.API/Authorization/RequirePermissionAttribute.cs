using HSAcademia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.API.Authorization;

/// <summary>Permisos granulares configurables por la academia en "Roles internos".</summary>
public static class Permissions
{
    public const string ViewStudents     = "view_students";
    public const string ManageAttendance = "manage_attendance";
    public const string ManageEvents     = "manage_events";
    public const string ManageStore      = "manage_store";
    public const string ManageFinances   = "manage_finances";
}

/// <summary>
/// Exige que el usuario tenga al menos UNO de los permisos indicados.
/// - AcademyAdmin y SuperAdmin pasan siempre.
/// - Staff: se valida contra los permisos de su AcademyRole (consultado en BD en cada request,
///   así un cambio de permisos aplica de inmediato sin esperar a que expire el token).
/// - Alumno / Apoderado: denegado.
/// Debe usarse junto con [Authorize]; este filtro solo evalúa permisos.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _anyOf;

    public RequirePermissionAttribute(params string[] anyOf) => _anyOf = anyOf;

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
            context.Result = Forbidden();
            return;
        }

        var userId = user.GetUserId();
        var academyId = user.GetAcademyId();
        if (userId == Guid.Empty || academyId == Guid.Empty)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var permissions = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.AcademyId == academyId)
            .Select(u => u.AcademyRole != null && u.AcademyRole.IsActive && !u.AcademyRole.IsDeleted
                ? u.AcademyRole.Permissions
                : null)
            .FirstOrDefaultAsync();

        if (permissions == null || !_anyOf.Any(permissions.Contains))
            context.Result = Forbidden();
    }

    private static ObjectResult Forbidden() =>
        new(new { message = "No tienes permisos para realizar esta acción." }) { StatusCode = StatusCodes.Status403Forbidden };
}

/// <summary>Consulta puntual de permisos dentro de una acción (misma regla que [RequirePermission]).</summary>
public static class PermissionChecker
{
    public static async Task<bool> HasAsync(HttpContext http, string permission)
    {
        var user = http.User;
        if (user.IsInRole("AcademyAdmin") || user.IsInRole("SuperAdmin")) return true;
        if (!user.IsInRole("Staff")) return false;
        var userId = user.GetUserId();
        var academyId = user.GetAcademyId();
        var db = http.RequestServices.GetRequiredService<AppDbContext>();
        var permissions = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.AcademyId == academyId)
            .Select(u => u.AcademyRole != null && u.AcademyRole.IsActive && !u.AcademyRole.IsDeleted
                ? u.AcademyRole.Permissions
                : null)
            .FirstOrDefaultAsync();
        return permissions != null && permissions.Contains(permission);
    }
}
