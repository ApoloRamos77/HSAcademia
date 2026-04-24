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
        var user = await _db.Users
            .Include(u => u.Academy)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == dto.Email.ToLower().Trim());

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

        return Result<LoginResponseDto>.Success(new LoginResponseDto
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            FullName = $"{user.FirstName} {user.LastName}",
            Role = user.Role.ToString(),
            AcademyId = user.AcademyId,
            AcademyName = user.Academy?.Name
        });
    }
}
