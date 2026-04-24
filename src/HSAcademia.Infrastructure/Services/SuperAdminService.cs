using HSAcademia.Application.Common;
using HSAcademia.Application.DTOs.SuperAdmin;
using HSAcademia.Application.Interfaces;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

public class SuperAdminService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;

    public SuperAdminService(AppDbContext db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    // =====================================================================
    // Registration Requests
    // =====================================================================

    public async Task<Result<Guid>> SubmitRegistrationRequestAsync(SubmitRegistrationRequestDto dto)
    {
        var exists = await _db.RegistrationRequests.AnyAsync(r =>
            r.ContactEmail == dto.ContactEmail.ToLower().Trim() &&
            r.Status == RegistrationRequestStatus.Pending);

        if (exists)
            return Result<Guid>.Failure("Ya existe una solicitud pendiente con este correo.");

        var request = new AcademyRegistrationRequest
        {
            AcademyName = dto.AcademyName.Trim(),
            Description = dto.Description?.Trim(),
            ContactName = dto.ContactName.Trim(),
            ContactEmail = dto.ContactEmail.ToLower().Trim(),
            ContactPhone = dto.ContactPhone?.Trim(),
            City = dto.City?.Trim(),
            Country = dto.Country?.Trim(),
            Sport = dto.Sport?.Trim(),
            Website = dto.Website?.Trim(),
            AdditionalInfo = dto.AdditionalInfo?.Trim()
        };

        _db.RegistrationRequests.Add(request);
        await _db.SaveChangesAsync();

        return Result<Guid>.Success(request.Id);
    }

    public async Task<List<RegistrationRequestListDto>> GetRegistrationRequestsAsync(RegistrationRequestStatus? status = null)
    {
        var query = _db.RegistrationRequests.AsQueryable();
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        return await query.OrderByDescending(r => r.CreatedAt)
            .Select(r => new RegistrationRequestListDto
            {
                Id = r.Id,
                AcademyName = r.AcademyName,
                ContactName = r.ContactName,
                ContactEmail = r.ContactEmail,
                ContactPhone = r.ContactPhone,
                City = r.City,
                Country = r.Country,
                Sport = r.Sport,
                Status = r.Status.ToString(),
                CreatedAt = r.CreatedAt,
                ReviewedAt = r.ReviewedAt
            })
            .ToListAsync();
    }

    public async Task<Result<RegistrationRequestDetailDto>> GetRegistrationRequestByIdAsync(Guid id)
    {
        var r = await _db.RegistrationRequests.FindAsync(id);
        if (r == null) return Result<RegistrationRequestDetailDto>.Failure("Solicitud no encontrada.");

        return Result<RegistrationRequestDetailDto>.Success(new RegistrationRequestDetailDto
        {
            Id = r.Id,
            AcademyName = r.AcademyName,
            Description = r.Description,
            ContactName = r.ContactName,
            ContactEmail = r.ContactEmail,
            ContactPhone = r.ContactPhone,
            City = r.City,
            Country = r.Country,
            Sport = r.Sport,
            Website = r.Website,
            AdditionalInfo = r.AdditionalInfo,
            Status = r.Status.ToString(),
            ReviewNotes = r.ReviewNotes,
            CreatedAt = r.CreatedAt,
            ReviewedAt = r.ReviewedAt,
            CreatedAcademyId = r.CreatedAcademyId
        });
    }

    public async Task<Result> EditRegistrationRequestAsync(Guid id, EditRegistrationRequestDto dto)
    {
        var request = await _db.RegistrationRequests.FindAsync(id);
        if (request == null) return Result.Failure("Solicitud no encontrada.");

        request.AcademyName = dto.AcademyName.Trim();
        request.Description = dto.Description?.Trim();
        request.ContactName = dto.ContactName.Trim();
        request.ContactEmail = dto.ContactEmail.ToLower().Trim();
        request.ContactPhone = dto.ContactPhone?.Trim();
        request.City = dto.City?.Trim();
        request.Country = dto.Country?.Trim();
        request.Sport = dto.Sport?.Trim();
        request.Website = dto.Website?.Trim();
        request.AdditionalInfo = dto.AdditionalInfo?.Trim();

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<AcademyCreatedDto>> ApproveRegistrationRequestAsync(Guid requestId, string reviewNotes, Guid reviewedByUserId)
    {
        var request = await _db.RegistrationRequests.FindAsync(requestId);
        if (request == null) return Result<AcademyCreatedDto>.Failure("Solicitud no encontrada.");
        if (request.Status != RegistrationRequestStatus.Pending)
            return Result<AcademyCreatedDto>.Failure("La solicitud ya fue procesada.");

        var slug = GenerateSlug(request.AcademyName);
        var slugExists = await _db.Academies.IgnoreQueryFilters().AnyAsync(a => a.SlugName == slug);
        if (slugExists) slug = $"{slug}-{DateTime.UtcNow.Ticks % 10000}";

        var academyTenantId = Guid.NewGuid();
        var academy = new Academy
        {
            AcademyId = academyTenantId,
            Name = request.AcademyName,
            SlugName = slug,
            Description = request.Description,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            City = request.City,
            Country = request.Country,
            Sport = request.Sport,
            Website = request.Website,
            Status = AcademyStatus.Active,
            ApprovedAt = DateTime.UtcNow
        };
        _db.Academies.Add(academy);

        var tempPassword = GenerateTempPassword();
        var nameParts = request.ContactName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var adminUser = new User
        {
            AcademyId = academyTenantId,
            FirstName = nameParts.FirstOrDefault() ?? request.ContactName,
            LastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "",
            Email = request.ContactEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword),
            Role = UserRole.AcademyAdmin,
            Status = UserStatus.Active
        };
        _db.Users.Add(adminUser);

        request.Status = RegistrationRequestStatus.Approved;
        request.ReviewNotes = reviewNotes;
        request.ReviewedByUserId = reviewedByUserId;
        request.ReviewedAt = DateTime.UtcNow;
        request.CreatedAcademy = academy;

        await _db.SaveChangesAsync();

        _ = _email.SendWelcomeEmailAsync(
            request.ContactEmail, academy.Name,
            $"{adminUser.FirstName} {adminUser.LastName}".Trim(),
            tempPassword);

        return Result<AcademyCreatedDto>.Success(new AcademyCreatedDto
        {
            AcademyId = academy.Id,
            AcademyTenantId = academyTenantId,
            SlugName = slug,
            AdminUserId = adminUser.Id,
            AdminEmail = adminUser.Email,
            TempPassword = tempPassword
        });
    }

    public async Task<Result> RejectRegistrationRequestAsync(Guid requestId, string reason, Guid reviewedByUserId)
    {
        var request = await _db.RegistrationRequests.FindAsync(requestId);
        if (request == null) return Result.Failure("Solicitud no encontrada.");
        if (request.Status != RegistrationRequestStatus.Pending)
            return Result.Failure("La solicitud ya fue procesada.");

        request.Status = RegistrationRequestStatus.Rejected;
        request.ReviewNotes = reason;
        request.ReviewedByUserId = reviewedByUserId;
        request.ReviewedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        _ = _email.SendRejectionEmailAsync(request.ContactEmail, request.AcademyName, reason);
        return Result.Success();
    }

    // =====================================================================
    // Academy Management
    // =====================================================================

    public async Task<List<AcademyListDto>> GetAcademiesAsync(string? statusFilter = null)
    {
        var query = _db.Academies.IgnoreQueryFilters().AsQueryable();

        if (!string.IsNullOrEmpty(statusFilter) &&
            Enum.TryParse<AcademyStatus>(statusFilter, true, out var parsed))
            query = query.Where(a => a.Status == parsed);

        return await query.OrderBy(a => a.Name).Select(a => new AcademyListDto
        {
            Id = a.Id,
            AcademyId = a.AcademyId,
            Name = a.Name,
            SlugName = a.SlugName,
            ContactEmail = a.ContactEmail,
            City = a.City,
            Country = a.Country,
            Sport = a.Sport,
            Status = a.Status.ToString(),
            UsersCount = a.Users.Count(u => !u.IsDeleted),
            CreatedAt = a.CreatedAt,
            ApprovedAt = a.ApprovedAt
        }).ToListAsync();
    }

    public async Task<Result<AcademyDetailDto>> GetAcademyByIdAsync(Guid id)
    {
        var a = await _db.Academies.IgnoreQueryFilters()
            .Include(x => x.Users.Where(u => !u.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == id);

        if (a == null) return Result<AcademyDetailDto>.Failure("Academia no encontrada.");

        return Result<AcademyDetailDto>.Success(new AcademyDetailDto
        {
            Id = a.Id,
            AcademyId = a.AcademyId,
            Name = a.Name,
            SlugName = a.SlugName,
            Description = a.Description,
            ContactEmail = a.ContactEmail,
            ContactPhone = a.ContactPhone,
            City = a.City,
            Country = a.Country,
            Sport = a.Sport,
            Website = a.Website,
            Status = a.Status.ToString(),
            SuspensionReason = a.SuspensionReason,
            CreatedAt = a.CreatedAt,
            ApprovedAt = a.ApprovedAt,
            SuspendedAt = a.SuspendedAt,
            Users = a.Users.Select(u => new UserSummaryDto
            {
                Id = u.Id,
                FullName = $"{u.FirstName} {u.LastName}",
                Email = u.Email,
                Role = u.Role.ToString(),
                Status = u.Status.ToString()
            }).ToList()
        });
    }

    public async Task<Result> EditAcademyAsync(Guid id, EditAcademyDto dto)
    {
        var academy = await _db.Academies.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == id);
        if (academy == null) return Result.Failure("Academia no encontrada.");

        academy.Name = dto.Name.Trim();
        academy.Description = dto.Description?.Trim();
        academy.ContactEmail = dto.ContactEmail.ToLower().Trim();
        academy.ContactPhone = dto.ContactPhone?.Trim();
        academy.City = dto.City?.Trim();
        academy.Country = dto.Country?.Trim();
        academy.Sport = dto.Sport?.Trim();
        academy.Website = dto.Website?.Trim();

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> SuspendAcademyAsync(Guid id, string reason, Guid byUserId)
    {
        var academy = await _db.Academies.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == id);
        if (academy == null) return Result.Failure("Academia no encontrada.");
        if (academy.Status == AcademyStatus.Suspended) return Result.Failure("La academia ya está suspendida.");

        academy.Status = AcademyStatus.Suspended;
        academy.SuspensionReason = reason;
        academy.SuspendedAt = DateTime.UtcNow;

        var users = await _db.Users.Where(u => u.AcademyId == academy.AcademyId).ToListAsync();
        foreach (var u in users) u.Status = UserStatus.Suspended;

        await _db.SaveChangesAsync();
        _ = _email.SendSuspensionEmailAsync(academy.ContactEmail, academy.Name, reason);
        return Result.Success();
    }

    public async Task<Result> ReactivateAcademyAsync(Guid id)
    {
        var academy = await _db.Academies.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == id);
        if (academy == null) return Result.Failure("Academia no encontrada.");

        academy.Status = AcademyStatus.Active;
        academy.SuspensionReason = null;
        academy.SuspendedAt = null;

        var users = await _db.Users.IgnoreQueryFilters()
            .Where(u => u.AcademyId == academy.AcademyId && !u.IsDeleted).ToListAsync();
        foreach (var u in users) u.Status = UserStatus.Active;

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> DeactivateAcademyAsync(Guid id, string reason)
    {
        var academy = await _db.Academies.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == id);
        if (academy == null) return Result.Failure("Academia no encontrada.");

        academy.Status = AcademyStatus.Inactive;
        academy.IsDeleted = true;
        academy.DeletedAt = DateTime.UtcNow;
        academy.SuspensionReason = reason;

        var users = await _db.Users.IgnoreQueryFilters()
            .Where(u => u.AcademyId == academy.AcademyId).ToListAsync();
        foreach (var u in users)
        {
            u.Status = UserStatus.Inactive;
            u.IsDeleted = true;
            u.DeletedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    // =====================================================================
    // User Management
    // =====================================================================

    public async Task<List<UserListDto>> GetUsersAsync(Guid? academyId = null, string? roleFilter = null)
    {
        var query = _db.Users.IgnoreQueryFilters().Include(u => u.Academy).AsQueryable();

        if (academyId.HasValue)
            query = query.Where(u => u.AcademyId == academyId);

        if (!string.IsNullOrEmpty(roleFilter) &&
            Enum.TryParse<UserRole>(roleFilter, true, out var parsedRole))
            query = query.Where(u => u.Role == parsedRole);

        return await query.OrderBy(u => u.LastName).Select(u => new UserListDto
        {
            Id = u.Id,
            FullName = $"{u.FirstName} {u.LastName}",
            Email = u.Email,
            Phone = u.Phone,
            Role = u.Role.ToString(),
            Status = u.Status.ToString(),
            AcademyId = u.AcademyId,
            AcademyName = u.Academy != null ? u.Academy.Name : null,
            LastLoginAt = u.LastLoginAt,
            CreatedAt = u.CreatedAt
        }).ToListAsync();
    }

    public async Task<Result> SuspendUserAsync(Guid userId, string reason)
    {
        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Result.Failure("Usuario no encontrado.");
        if (user.Role == UserRole.SuperAdmin) return Result.Failure("No se puede suspender al Super Admin.");

        user.Status = UserStatus.Suspended;
        user.SuspensionReason = reason;
        user.SuspendedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> ReactivateUserAsync(Guid userId)
    {
        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Result.Failure("Usuario no encontrado.");

        user.Status = UserStatus.Active;
        user.SuspensionReason = null;
        user.SuspendedAt = null;

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> DeactivateUserAsync(Guid userId, string reason)
    {
        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Result.Failure("Usuario no encontrado.");
        if (user.Role == UserRole.SuperAdmin) return Result.Failure("No se puede dar de baja al Super Admin.");

        user.Status = UserStatus.Inactive;
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.SuspensionReason = reason;

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    // =====================================================================
    // Dashboard
    // =====================================================================

    public async Task<DashboardStatsDto> GetDashboardStatsAsync()
    {
        return new DashboardStatsDto
        {
            TotalAcademies = await _db.Academies.IgnoreQueryFilters().CountAsync(),
            ActiveAcademies = await _db.Academies.IgnoreQueryFilters().CountAsync(a => a.Status == AcademyStatus.Active),
            SuspendedAcademies = await _db.Academies.IgnoreQueryFilters().CountAsync(a => a.Status == AcademyStatus.Suspended),
            PendingRequests = await _db.RegistrationRequests.CountAsync(r => r.Status == RegistrationRequestStatus.Pending),
            TotalUsers = await _db.Users.IgnoreQueryFilters().CountAsync(u => u.Role != UserRole.SuperAdmin),
            ActiveUsers = await _db.Users.IgnoreQueryFilters().CountAsync(u => u.Status == UserStatus.Active && u.Role != UserRole.SuperAdmin),
        };
    }

    // =====================================================================
    // Helpers
    // =====================================================================
    private static string GenerateSlug(string name)
        => new string(name.ToLower()
            .Replace(" ", "-").Replace("á", "a").Replace("é", "e")
            .Replace("í", "i").Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n")
            .Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

    private static string GenerateTempPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 12).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
