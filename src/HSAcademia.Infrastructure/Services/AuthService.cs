using HSAcademia.Application.Common;
using HSAcademia.Application.DTOs.Auth;
using HSAcademia.Application.Interfaces;
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

    public AuthService(AppDbContext db, IJwtService jwt)
    {
        _db = db;
        _jwt = jwt;
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
            .Include(u => u.AcademyRole)
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
            return Result<LoginResponseDto>.Failure(InvalidCredentials);

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
        await _db.SaveChangesAsync();

        var token = await _jwt.GenerateTokenAsync(user);

        // Para Alumno/Apoderado: categoría del alumno vinculado
        Guid? categoryId = null;
        if (user.Role == UserRole.Student || user.Role == UserRole.Guardian)
        {
            categoryId = await _db.Students
                .Where(s => s.AcademyId == user.AcademyId && (s.UserId == user.Id || s.GuardianId == user.Id))
                .Select(s => (Guid?)s.CategoryId)
                .FirstOrDefaultAsync();
        }

        return Result<LoginResponseDto>.Success(new LoginResponseDto
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
            Permissions = user.AcademyRole?.Permissions ?? new List<string>()
        });
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
