using HSAcademia.Application.Common;
using HSAcademia.Application.DTOs.Auth;
using HSAcademia.Application.Interfaces;
using HSAcademia.Domain.Authorization;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

public class AuthService
{
    private const string InvalidCredentials = "Credenciales incorrectas.";

    private readonly AppDbContext _db;
    private readonly IJwtService _jwt;
    private readonly AccessScopeService _scope;
    private readonly AuditService _audit;

    public AuthService(AppDbContext db, IJwtService jwt, AccessScopeService scope, AuditService audit)
    {
        _audit = audit;
        _db = db;
        _jwt = jwt;
        _scope = scope;
    }

    public async Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto dto)
    {
        var query = (dto.EmailOrPhone ?? string.Empty).Trim().ToLower();
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(dto.Password))
            return Result<LoginResponseDto>.Failure(InvalidCredentials);

        // El teléfono y el DNI no son únicos entre academias: se buscan todos los candidatos
        // y se elige el que coincide con la contraseña (y con la academia, si se indicó).
        var candidatesQuery = _db.Users
            .Include(u => u.Academy)
            .Where(u => u.Email.ToLower() == query || u.Phone == query || u.DocumentNumber == query);

        if (dto.AcademyId.HasValue && dto.AcademyId.Value != Guid.Empty)
        {
            var selected = dto.AcademyId.Value;
            candidatesQuery = candidatesQuery.Where(u =>
                u.Role == UserRole.SuperAdmin ||
                u.AcademyId == selected ||
                (u.Academy != null && u.Academy.Id == selected));
        }

        var candidates = await candidatesQuery.Take(10).ToListAsync();

        // Verificar la contraseña ANTES de dar cualquier otro detalle, para no revelar si una cuenta existe.
        var matches = candidates.Where(u => BCrypt.Net.BCrypt.Verify(dto.Password, u.PasswordHash)).ToList();

        if (matches.Count == 0)
        {
            // Solo el equipo de la academia: los ingresos de familias no aportan a la seguridad y llenarían la bitácora.
            var attempted = candidates.Where(IsTeam).ToList();
            foreach (var candidate in attempted)
                _audit.Add(candidate.AcademyId, "auth.login_failed", nameof(User), candidate.Id, actorUserId: candidate.Id);
            if (attempted.Count > 0) await _db.SaveChangesAsync();
            return Result<LoginResponseDto>.Failure(InvalidCredentials);
        }

        if (matches.Count > 1)
        {
            // Si se indicó un rol, intentar desambiguar con él.
            var byRole = matches.Where(u => RoleMatches(dto.Role, u.Role)).ToList();
            if (byRole.Count == 1) matches = byRole;
            else
                return Result<LoginResponseDto>.Failure(
                    "Tu usuario está registrado en más de una academia. Selecciona la academia para continuar.");
        }

        var user = matches[0];

        if (!string.IsNullOrEmpty(dto.Role) && !RoleMatches(dto.Role, user.Role))
            return Result<LoginResponseDto>.Failure($"No tienes permisos para ingresar con el rol de {dto.Role}.");

        if (user.Status == UserStatus.Suspended)
            return Result<LoginResponseDto>.Failure($"Su cuenta está suspendida. {user.SuspensionReason}".Trim());

        if (user.Status == UserStatus.Inactive)
            return Result<LoginResponseDto>.Failure("Su cuenta está inactiva. Contacte al administrador.");

        if (user.AcademyId != null)
        {
            if (user.Academy == null)
                return Result<LoginResponseDto>.Failure("La academia no está disponible.");
            switch (user.Academy.Status)
            {
                case AcademyStatus.Suspended:
                    return Result<LoginResponseDto>.Failure("La academia está suspendida temporalmente.");
                case AcademyStatus.Inactive:
                    return Result<LoginResponseDto>.Failure("La academia ha sido dada de baja.");
                case AcademyStatus.Pending:
                case AcademyStatus.Rejected:
                    return Result<LoginResponseDto>.Failure("La academia no se encuentra activa.");
            }
        }

        // Contraseña temporal o por defecto (usamos el texto plano ya recibido: sin hashes extra).
        if (PasswordPolicy.IsWeak(dto.Password))
            user.MustChangePassword = true;

        user.LastLoginAt = DateTime.UtcNow;
        if (IsTeam(user))
            _audit.Add(user.AcademyId, "auth.login", nameof(User), user.Id, actorUserId: user.Id);
        await _db.SaveChangesAsync();

        var token = await _jwt.GenerateTokenAsync(user);

        return Result<LoginResponseDto>.Success(await BuildResponseAsync(user, token));
    }

    /// <summary>Datos y permisos vigentes del usuario, sin emitir un token nuevo.</summary>
    public async Task<LoginResponseDto?> GetCurrentAsync(Guid userId)
    {
        var user = await _db.Users.AsNoTracking().Include(u => u.Academy).FirstOrDefaultAsync(u => u.Id == userId);
        return user == null ? null : await BuildResponseAsync(user, string.Empty);
    }

    private async Task<LoginResponseDto> BuildResponseAsync(User user, string token)
    {
        // Para Alumno/Apoderado: categoría del alumno vinculado
        Guid? categoryId = null;
        if (user.Role == UserRole.Student || user.Role == UserRole.Guardian)
        {
            categoryId = await _db.Students
                .Where(s => s.AcademyId == user.AcademyId && (s.UserId == user.Id || s.GuardianId == user.Id))
                .Select(s => (Guid?)s.CategoryId)
                .FirstOrDefaultAsync();
        }

        var scope = await _scope.ForUserAsync(user.Id);
        return new LoginResponseDto
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            FullName = $"{user.FirstName} {user.LastName}",
            Role = user.Role.ToString(),
            AcademyId = user.AcademyId,
            AcademyName = user.Academy?.Name,
            RequirePasswordChange = user.MustChangePassword,
            CategoryId = categoryId,
            Permissions = Permissions.WithLegacyAliases(scope.Permissions),
            IsOwner = user.IsOwner,
            AcademyWide = scope.IsAcademyWide
        };
    }

    private static bool IsTeam(User user) => user.Role is UserRole.AcademyAdmin or UserRole.Staff or UserRole.SuperAdmin;

    public async Task<ProfileDto?> GetProfileAsync(Guid userId)
    {
        var user = await _db.Users.AsNoTracking()
            .Include(u => u.Academy)
            .Include(u => u.AssignedRoles)
            .Include(u => u.AssignedHeadquarters)
            .Include(u => u.AssignedCategories)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return null;

        var scope = await _scope.ForUserAsync(userId);
        var logins = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.UserId == userId && a.Action == "auth.login")
            .OrderByDescending(a => a.CreatedAt)
            .Take(5)
            .Select(a => new { a.CreatedAt, a.UserAgent, a.IpAddress })
            .ToListAsync();

        return new ProfileDto
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = StudentService.IsPlaceholderEmail(user.Email) ? "" : user.Email,
            Phone = user.Phone,
            DocumentNumber = user.DocumentNumber,
            Role = user.Role.ToString(),
            IsOwner = user.IsOwner,
            AcademyName = user.Academy?.Name,
            FullAccess = scope.IsAdmin,
            Roles = user.AssignedRoles.Where(r => r.IsActive).Select(r => r.Name).OrderBy(n => n).ToList(),
            Headquarters = user.AssignedHeadquarters.Select(h => h.Name).OrderBy(n => n).ToList(),
            Categories = user.AssignedCategories.Select(c => c.Name).OrderBy(n => n).ToList(),
            PermissionGroups = Permissions.Catalog
                .Select(g => new ProfilePermissionGroupDto
                {
                    Name = g.Name,
                    Items = g.Items.Where(i => scope.Permissions.Contains(i.Id)).Select(i => i.Label).ToList()
                })
                .Where(g => g.Items.Count > 0)
                .ToList(),
            RecentLogins = logins.Select(l => new ProfileLoginDto
            {
                At = DateTime.SpecifyKind(l.CreatedAt, DateTimeKind.Utc),
                Device = ActivityService.DeviceOf(l.UserAgent),
                IpAddress = l.IpAddress
            }).ToList()
        };
    }

    public async Task<Result<bool>> UpdateProfileAsync(Guid userId, UpdateProfileDto dto)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Result<bool>.Failure("Usuario no encontrado.");

        var firstName = dto.FirstName?.Trim();
        var lastName = dto.LastName?.Trim();
        var phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
            return Result<bool>.Failure("El nombre y el apellido son obligatorios.");
        if (firstName.Length > 100 || lastName.Length > 100 || phone is { Length: > 50 })
            return Result<bool>.Failure("Alguno de los datos es demasiado largo.");

        if (user.FirstName == firstName && user.LastName == lastName && user.Phone == phone)
            return Result<bool>.Success(true);

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Phone = phone;
        if (IsTeam(user))
            _audit.Add(user.AcademyId, "users.profile.updated", nameof(User), user.Id, new { nombre = $"{firstName} {lastName}", telefono = phone });
        await _db.SaveChangesAsync();
        return Result<bool>.Success(true);
    }

    private static bool RoleMatches(string? requested, UserRole role)
    {
        if (string.IsNullOrEmpty(requested)) return true;
        return requested.ToUpperInvariant() switch
        {
            "STAFF"      => role == UserRole.AcademyAdmin || role == UserRole.Staff,
            "ALUMNO"     => role == UserRole.Student,
            "APODERADO"  => role == UserRole.Guardian,
            "SUPERADMIN" => role == UserRole.SuperAdmin,
            _            => false
        };
    }

    public async Task<Result<bool>> ChangePasswordAsync(Guid userId, ChangePasswordRequestDto dto)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Result<bool>.Failure("Usuario no encontrado.");

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            return Result<bool>.Failure("La contraseña actual es incorrecta.");

        var policyError = PasswordPolicy.Validate(dto.NewPassword);
        if (policyError != null) return Result<bool>.Failure(policyError);

        if (dto.NewPassword == dto.CurrentPassword)
            return Result<bool>.Failure("La nueva contraseña debe ser distinta de la actual.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.MustChangePassword = false;
        if (IsTeam(user))
            _audit.Add(user.AcademyId, "auth.password.changed", nameof(User), user.Id, actorUserId: user.Id);
        await _db.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    /// <summary>Asigna una contraseña temporal aleatoria y obliga a cambiarla. Devuelve la contraseña en texto plano.</summary>
    public static string AssignTemporaryPassword(User user)
    {
        var temp = PasswordPolicy.GenerateTemporary();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temp);
        user.MustChangePassword = true;
        return temp;
    }
}
