using System.Security.Claims;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>Lo que una persona puede hacer (permisos sumados de sus roles) y sobre qué (sedes y categorías).</summary>
public sealed record AccessScope(
    bool IsAdmin,
    IReadOnlySet<string> Permissions,
    IReadOnlyList<Guid>? Headquarters,
    IReadOnlyList<Guid> Categories)
{
    public bool Has(params string[] anyOf) => IsAdmin || anyOf.Any(Permissions.Contains);

    /// <summary>Sin sedes asignadas alcanza todas.</summary>
    public bool IsAcademyWide => IsAdmin || Headquarters == null;

    public bool CoversHeadquarter(Guid headquarterId) => IsAcademyWide || Headquarters!.Contains(headquarterId);

    public bool SeesWholeSede => Has(HSAcademia.Domain.Authorization.Permissions.SedeWideStudentAccess);
}

/// <summary>
/// Calcula el alcance desde la base en cada petición, de modo que un cambio de roles o sedes aplica de inmediato.
/// Regla: lo deportivo se limita por categorías; cobranza y reportes, por sedes.
/// </summary>
public class AccessScopeService
{
    private static readonly AccessScope Empty =
        new(false, new HashSet<string>(), Array.Empty<Guid>(), Array.Empty<Guid>());

    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;
    private AccessScope? _current;

    public AccessScopeService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task<AccessScope> ForUserAsync(Guid userId)
    {
        var account = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Role,
                Roles = u.AssignedRoles.Where(r => r.IsActive && !r.IsDeleted).Select(r => r.Permissions).ToList(),
                Headquarters = u.AssignedHeadquarters.Select(h => h.Id).ToList(),
                Categories = u.AssignedCategories.Select(c => c.Id).ToList()
            })
            .FirstOrDefaultAsync();
        if (account == null) return Empty;

        return new AccessScope(
            account.Role is UserRole.AcademyAdmin or UserRole.SuperAdmin,
            account.Roles.SelectMany(p => p).ToHashSet(),
            account.Headquarters.Count == 0 ? null : account.Headquarters,
            account.Categories);
    }

    public async Task<AccessScope> CurrentAsync()
    {
        if (_current != null) return _current;
        var value = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        _current = Guid.TryParse(value, out var userId) ? await ForUserAsync(userId) : Empty;
        return _current;
    }

    /// <summary>Sedes a las que se limita el personal de esta petición; null si no aplica ningún límite.</summary>
    public async Task<IReadOnlyList<Guid>?> StaffHeadquartersAsync()
    {
        if (_http.HttpContext?.User.IsInRole("Staff") != true) return null;
        return (await CurrentAsync()).Headquarters;
    }

    /// <summary>Alumnos visibles en listados: sus categorías; sin categorías, toda su sede si atiende cobros o ventas.</summary>
    public static IQueryable<Student> ApplyStudents(IQueryable<Student> query, AccessScope scope)
    {
        if (scope.IsAdmin) return query;
        if (scope.Categories.Count > 0)
        {
            var categories = scope.Categories;
            return query.Where(s => categories.Contains(s.CategoryId));
        }
        if (!scope.SeesWholeSede) return query.Where(s => false);
        if (scope.Headquarters == null) return query;
        var headquarters = scope.Headquarters;
        return query.Where(s => headquarters.Contains(s.HeadquarterId));
    }
}
