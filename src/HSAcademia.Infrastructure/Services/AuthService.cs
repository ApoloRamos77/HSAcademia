using HSAcademia.Application.Common;
using HSAcademia.Application.DTOs.Auth;
using HSAcademia.Application.Interfaces;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwt;

    public AuthService(AppDbContext db, IJwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto dto)
    {
        var query = dto.EmailOrPhone.ToLower().Trim();
        var user = await _db.Users
            .Include(u => u.Academy)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == query || u.Phone == query);

        if (user == null || user.IsDeleted)
            return Result<LoginResponseDto>.Failure("Credenciales incorrectas.");

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Result<LoginResponseDto>.Failure("Credenciales incorrectas.");

        if (user.Status == UserStatus.Suspended)
            return Result<LoginResponseDto>.Failure($"Su cuenta está suspendida. {user.SuspensionReason}");

        if (user.Status == UserStatus.Inactive)
            return Result<LoginResponseDto>.Failure("Su cuenta está inactiva. Contacte al administrador.");

        if (user.AcademyId != null && user.Academy != null)
        {
            if (user.Academy.Status == AcademyStatus.Suspended)
                return Result<LoginResponseDto>.Failure("La academia está suspendida temporalmente.");
            if (user.Academy.Status == AcademyStatus.Inactive)
                return Result<LoginResponseDto>.Failure("La academia ha sido dada de baja.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = _jwt.GenerateToken(user);
        var requirePasswordChange = BCrypt.Net.BCrypt.Verify("123456", user.PasswordHash) || BCrypt.Net.BCrypt.Verify("12345", user.PasswordHash);

        return Result<LoginResponseDto>.Success(new LoginResponseDto
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            FullName = $"{user.FirstName} {user.LastName}",
            Role = user.Role.ToString(),
            AcademyId = user.AcademyId,
            AcademyName = user.Academy?.Name,
            RequirePasswordChange = requirePasswordChange
        });
    }

    public async Task<Result<bool>> ChangePasswordAsync(Guid userId, ChangePasswordRequestDto dto)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Result<bool>.Failure("Usuario no encontrado.");

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            return Result<bool>.Failure("La contraseña actual es incorrecta.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await _db.SaveChangesAsync();

        return Result<bool>.Success(true);
    }
}
