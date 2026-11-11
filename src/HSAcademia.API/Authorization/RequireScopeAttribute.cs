using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HSAcademia.API.Authorization;

public enum ScopeTarget { Student, Category, Event, Debt, Proof, Receipt }

/// <summary>
/// Reglas de alcance del personal: el permiso dice QUÉ puede hacer; el alcance, SOBRE QUÉ registros.
/// Lo deportivo se limita por categorías y lo financiero por sedes. El administrador alcanza toda la academia,
/// incluidos los alumnos retirados; el resto del personal solo alcanza a los activos.
/// </summary>
public static class ScopeChecker
{
    public const string DeniedMessage = "Este registro está fuera de tu alcance asignado.";

    public static ObjectResult Denied() =>
        new(new { message = DeniedMessage }) { StatusCode = StatusCodes.Status403Forbidden };

    /// <summary>Un alumno solo se inscribe o se mueve a una categoría y sede dentro del alcance de quien lo hace.</summary>
    public static async Task<bool> CanPlaceStudentAsync(HttpContext http, Guid categoryId, Guid headquarterId)
    {
        if (http.User.IsInRole("AcademyAdmin") || http.User.IsInRole("SuperAdmin")) return true;
        var scope = await http.RequestServices.GetRequiredService<AccessScopeService>().CurrentAsync();
        return scope.Categories.Count > 0
            ? scope.Categories.Contains(categoryId)
            : scope.SeesWholeSede && scope.CoversHeadquarter(headquarterId);
    }

    public static async Task<bool> IsAllowedAsync(HttpContext http, ScopeTarget target, Guid id, params string[] sedeWidePermissions)
    {
        var user = http.User;
        if (user.IsInRole("AcademyAdmin") || user.IsInRole("SuperAdmin")) return true;
        if (!user.IsInRole("Staff")) return false;

        var scope = await http.RequestServices.GetRequiredService<AccessScopeService>().CurrentAsync();
        var access = http.RequestServices.GetRequiredService<AccessControlService>();
        var academyId = user.GetAcademyId();
        var userId = user.GetUserId();
        return target switch
        {
            ScopeTarget.Student =>
                await access.StudentIsActiveAsync(academyId, id)
                && (await access.StaffCanAccessStudentAsync(academyId, userId, id)
                    || (scope.Has(sedeWidePermissions) && await access.StudentInScopeAsync(academyId, id, scope))),
            ScopeTarget.Category => await access.StaffCanAccessCategoryAsync(academyId, userId, id),
            ScopeTarget.Event => await access.StaffCanAccessEventAsync(academyId, userId, id),
            ScopeTarget.Debt => await access.DebtInScopeAsync(academyId, id, scope),
            ScopeTarget.Proof => await access.ProofInScopeAsync(academyId, id, scope),
            ScopeTarget.Receipt => await access.ReceiptInScopeAsync(academyId, id, scope),
            _ => false
        };
    }
}

/// <summary>
/// Exige que el registro indicado en la ruta esté dentro del alcance del usuario.
/// sedeWidePermissions: permisos con los que un alumno basta que sea de sus sedes, aunque no de sus categorías.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireScopeAttribute : Attribute, IAsyncActionFilter
{
    private readonly ScopeTarget _target;
    private readonly string _routeParameter;
    private readonly string[] _sedeWidePermissions;

    public RequireScopeAttribute(ScopeTarget target, string routeParameter, params string[] sedeWidePermissions)
    {
        _target = target;
        _routeParameter = routeParameter;
        _sedeWidePermissions = sedeWidePermissions;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!Guid.TryParse(context.RouteData.Values[_routeParameter]?.ToString(), out var id)
            || !await ScopeChecker.IsAllowedAsync(context.HttpContext, _target, id, _sedeWidePermissions))
        {
            context.Result = ScopeChecker.Denied();
            return;
        }

        await next();
    }
}
