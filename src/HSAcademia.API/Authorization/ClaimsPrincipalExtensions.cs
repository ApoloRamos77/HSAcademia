using System.Security.Claims;

namespace HSAcademia.API.Authorization;

/// <summary>
/// Lectura centralizada de los claims del JWT.
/// El token emite "sub" (mapeado por JwtBearer a ClaimTypes.NameIdentifier), "academyId" y el rol.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    public static Guid GetAcademyId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("academyId") ?? user.FindFirstValue("AcademyId");
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    public static string GetRole(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Role) ?? user.FindFirstValue("role") ?? string.Empty;

    /// <summary>True para Alumno o Apoderado (usuarios finales de la app móvil).</summary>
    public static bool IsEndUser(this ClaimsPrincipal user)
        => user.IsInRole("Student") || user.IsInRole("Guardian");
}
