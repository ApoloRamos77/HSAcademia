using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Configuration;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

public class AcademyConfigurationService
{
    private readonly AppDbContext _context;
    private readonly AuditService _audit;

    public AcademyConfigurationService(AppDbContext context, AuditService audit)
    {
        _context = context;
        _audit = audit;
    }

    // ==========================================
    // Headquarters (Sedes)
    // ==========================================
    public async Task<List<HeadquarterDto>> GetHeadquartersAsync(Guid academyId)
    {
        return await _context.Headquarters
            .Where(h => h.AcademyId == academyId)
            .Select(h => new HeadquarterDto
            {
                Id = h.Id,
                Name = h.Name,
                Address = h.Address,
                ContactPhone = h.ContactPhone,
                IsActive = h.IsActive
            })
            .ToListAsync();
    }

    public async Task<HeadquarterDto> CreateHeadquarterAsync(Guid academyId, CreateHeadquarterDto dto)
    {
        var headquarter = new Headquarter
        {
            AcademyId = academyId,
            Name = dto.Name,
            Address = dto.Address,
            ContactPhone = dto.ContactPhone
        };
        _context.Headquarters.Add(headquarter);
        await _context.SaveChangesAsync();
        return new HeadquarterDto { Id = headquarter.Id, Name = headquarter.Name, Address = headquarter.Address, ContactPhone = headquarter.ContactPhone, IsActive = headquarter.IsActive };
    }

    public async Task DeleteHeadquarterAsync(Guid academyId, Guid id)
    {
        var headquarter = await _context.Headquarters.FirstOrDefaultAsync(h => h.Id == id && h.AcademyId == academyId);
        if (headquarter != null)
        {
            headquarter.IsDeleted = true;
            headquarter.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<HeadquarterDto> UpdateHeadquarterAsync(Guid academyId, Guid id, CreateHeadquarterDto dto)
    {
        var headquarter = await _context.Headquarters.FirstOrDefaultAsync(h => h.Id == id && h.AcademyId == academyId);
        if (headquarter == null) throw new Exception("Sede no encontrada.");
        headquarter.Name = dto.Name;
        headquarter.Address = dto.Address;
        headquarter.ContactPhone = dto.ContactPhone;
        await _context.SaveChangesAsync();
        return new HeadquarterDto { Id = headquarter.Id, Name = headquarter.Name, Address = headquarter.Address, ContactPhone = headquarter.ContactPhone, IsActive = headquarter.IsActive };
    }

    // ==========================================
    // Categories (Categorías)
    // ==========================================
    public async Task<List<CategoryDto>> GetCategoriesAsync(Guid academyId, Guid? userId = null, string userRole = "")
    {
        var query = _context.Categories
            .Include(c => c.Headquarter)
            .Where(c => c.AcademyId == academyId);

        if (userRole == "Staff" && userId.HasValue)
        {
            var userWithCategories = await _context.Users
                .Include(u => u.AssignedCategories)
                .FirstOrDefaultAsync(u => u.Id == userId.Value);

            if (userWithCategories != null && userWithCategories.AssignedCategories.Any())
            {
                var assignedCategoryIds = userWithCategories.AssignedCategories.Select(c => c.Id).ToList();
                query = query.Where(c => assignedCategoryIds.Contains(c.Id));
            }
            else
            {
                // Si es staff pero no tiene categorias asignadas, no ve ninguna categoria.
                return new List<CategoryDto>();
            }
        }

        return await query
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                HeadquarterId = c.HeadquarterId,
                HeadquarterName = c.Headquarter!.Name,
                Name = c.Name,
                StartDateOfBirth = c.StartDateOfBirth,
                EndDateOfBirth = c.EndDateOfBirth,
                MonthlyFee = c.MonthlyFee,
                IsActive = c.IsActive
            })
            .ToListAsync();
    }

    private static void ValidateCategory(CreateCategoryDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new Exception("El nombre de la categoría es obligatorio.");
        if (dto.MonthlyFee.HasValue && dto.MonthlyFee.Value < 0) throw new Exception("La mensualidad no puede ser negativa.");
        if (dto.EndDateOfBirth < dto.StartDateOfBirth) throw new Exception("La fecha de nacimiento final debe ser posterior a la inicial.");
    }

    public async Task<CategoryDto> CreateCategoryAsync(Guid academyId, CreateCategoryDto dto)
    {
        ValidateCategory(dto);
        var hq = await _context.Headquarters.FirstOrDefaultAsync(h => h.Id == dto.HeadquarterId && h.AcademyId == academyId);
        if (hq == null) throw new Exception("Sede no encontrada.");

        var category = new Category
        {
            AcademyId = academyId,
            HeadquarterId = dto.HeadquarterId,
            Name = dto.Name,
            StartDateOfBirth = dto.StartDateOfBirth.ToUniversalTime(),
            EndDateOfBirth = dto.EndDateOfBirth.ToUniversalTime(),
            MonthlyFee = dto.MonthlyFee ?? 0m
        };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        return new CategoryDto { Id = category.Id, HeadquarterId = category.HeadquarterId, HeadquarterName = hq.Name, Name = category.Name, StartDateOfBirth = category.StartDateOfBirth, EndDateOfBirth = category.EndDateOfBirth, MonthlyFee = category.MonthlyFee, IsActive = category.IsActive };
    }

    public async Task<CategoryDto> UpdateCategoryAsync(Guid academyId, Guid id, CreateCategoryDto dto)
    {
        ValidateCategory(dto);
        var category = await _context.Categories.Include(c => c.Headquarter).FirstOrDefaultAsync(c => c.Id == id && c.AcademyId == academyId);
        if (category == null) throw new Exception("Categoría no encontrada.");
        var hq = await _context.Headquarters.FirstOrDefaultAsync(h => h.Id == dto.HeadquarterId && h.AcademyId == academyId);
        if (hq == null) throw new Exception("Sede no encontrada.");

        category.HeadquarterId = dto.HeadquarterId;
        category.Name = dto.Name;
        category.StartDateOfBirth = dto.StartDateOfBirth.ToUniversalTime();
        category.EndDateOfBirth = dto.EndDateOfBirth.ToUniversalTime();
        if (dto.MonthlyFee.HasValue) category.MonthlyFee = dto.MonthlyFee.Value;
        await _context.SaveChangesAsync();
        return new CategoryDto { Id = category.Id, HeadquarterId = category.HeadquarterId, HeadquarterName = hq.Name, Name = category.Name, StartDateOfBirth = category.StartDateOfBirth, EndDateOfBirth = category.EndDateOfBirth, MonthlyFee = category.MonthlyFee, IsActive = category.IsActive };
    }

    // ==========================================
    // Academy Roles (Roles personalizados)
    // ==========================================
    public async Task<List<AcademyRoleDto>> GetRolesAsync(Guid academyId)
    {
        return await _context.AcademyRoles
            .Where(r => r.AcademyId == academyId)
            .Select(r => new AcademyRoleDto { Id = r.Id, Name = r.Name, Description = r.Description, Permissions = r.Permissions, IsActive = r.IsActive })
            .ToListAsync();
    }

    public async Task<AcademyRoleDto> CreateRoleAsync(Guid academyId, CreateAcademyRoleDto dto)
    {
        var role = new AcademyRole { AcademyId = academyId, Name = dto.Name, Description = dto.Description, Permissions = dto.Permissions ?? new List<string>() };
        _context.AcademyRoles.Add(role);
        await _context.SaveChangesAsync();
        return new AcademyRoleDto { Id = role.Id, Name = role.Name, Description = role.Description, Permissions = role.Permissions, IsActive = role.IsActive };
    }

    public async Task<AcademyRoleDto> UpdateRoleAsync(Guid academyId, Guid id, CreateAcademyRoleDto dto)
    {
        var role = await _context.AcademyRoles.FirstOrDefaultAsync(r => r.Id == id && r.AcademyId == academyId);
        if (role == null) throw new Exception("Rol no encontrado.");
        role.Name = dto.Name;
        role.Description = dto.Description;
        role.Permissions = dto.Permissions ?? new List<string>();
        await _context.SaveChangesAsync();
        return new AcademyRoleDto { Id = role.Id, Name = role.Name, Description = role.Description, Permissions = role.Permissions, IsActive = role.IsActive };
    }

    // ==========================================
    // Academy Users (Usuarios y roles)
    // ==========================================
    public async Task<List<AcademyUserDto>> GetUsersAsync(Guid academyId)
    {
        return await _context.Users
            .Include(u => u.AcademyRole)
            .Include(u => u.Headquarter)
            .Include(u => u.AssignedCategories)
            .Where(u => u.AcademyId == academyId)
            .Select(u => new AcademyUserDto
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = StudentService.IsPlaceholderEmail(u.Email) ? "" : u.Email,
                Phone = u.Phone,
                SystemRole = u.Role.ToString(),
                AcademyRoleId = u.AcademyRoleId,
                AcademyRoleName = u.AcademyRole != null ? u.AcademyRole.Name : null,
                HeadquarterId = u.HeadquarterId,
                HeadquarterName = u.Headquarter != null ? u.Headquarter.Name : null,
                Status = u.Status.ToString(),
                CategoryIds = u.AssignedCategories.Select(c => c.Id).ToList(),
                CategoryNames = u.AssignedCategories.Select(c => c.Name).ToList(),
                PaymentType = (int)u.PaymentType,
                PaymentRate = u.PaymentRate
            })
            .ToListAsync();
    }

    /// <summary>Roles que un administrador de academia puede asignar. Nunca SuperAdmin.</summary>
    private static readonly UserRole[] AssignableRoles = { UserRole.AcademyAdmin, UserRole.Staff, UserRole.Guardian, UserRole.Student };

    private static UserRole ParseAssignableRole(string? value, UserRole fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        // Solo nombres (no números) y solo roles permitidos para una academia.
        if (int.TryParse(value, out _) || !Enum.TryParse<UserRole>(value, true, out var role) || !AssignableRoles.Contains(role))
            throw new Exception("Rol de sistema no válido.");
        return role;
    }

    private Task<bool> EmailInUseAsync(string email, Guid? exceptUserId = null)
    {
        var normalized = email.Trim().ToLower();
        // IgnoreQueryFilters: el índice único de email también incluye usuarios eliminados.
        return _context.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Email.ToLower() == normalized && (exceptUserId == null || u.Id != exceptUserId));
    }

    private async Task ValidateUserReferencesAsync(Guid academyId, Guid? academyRoleId, Guid? headquarterId)
    {
        if (academyRoleId.HasValue && !await _context.AcademyRoles.AnyAsync(r => r.Id == academyRoleId && r.AcademyId == academyId))
            throw new Exception("El cargo seleccionado no es válido.");
        if (headquarterId.HasValue && !await _context.Headquarters.AnyAsync(h => h.Id == headquarterId && h.AcademyId == academyId))
            throw new Exception("La sede seleccionada no es válida.");
    }

    public async Task<AcademyUserDto> CreateUserAsync(Guid academyId, CreateAcademyUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email)) throw new Exception("El correo es obligatorio.");
        if (await EmailInUseAsync(dto.Email))
            throw new Exception("El correo ya está en uso.");

        var sysRole = ParseAssignableRole(dto.SystemRole, UserRole.Staff);
        await ValidateUserReferencesAsync(academyId, dto.AcademyRoleId, dto.HeadquarterId);

        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            throw new Exception("La contraseña inicial debe tener al menos 6 caracteres.");

        var user = new User
        {
            AcademyId = academyId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email.Trim().ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            // La contraseña la definió el administrador: el usuario debe cambiarla al ingresar.
            MustChangePassword = true,
            Phone = dto.Phone,
            Role = sysRole,
            AcademyRoleId = dto.AcademyRoleId,
            HeadquarterId = dto.HeadquarterId,
            Status = UserStatus.Active,
            PaymentType = (StaffPaymentType)dto.PaymentType,
            PaymentRate = dto.PaymentRate
        };

        if (dto.CategoryIds != null && dto.CategoryIds.Any())
        {
            var categories = await _context.Categories
                .Where(c => c.AcademyId == academyId && dto.CategoryIds.Contains(c.Id))
                .ToListAsync();
            user.AssignedCategories = categories;
        }

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return new AcademyUserDto { Id = user.Id, FirstName = user.FirstName, LastName = user.LastName, Email = user.Email };
    }

    public async Task<AcademyUserDto> UpdateUserAsync(Guid academyId, Guid id, CreateAcademyUserDto dto)
    {
        var user = await _context.Users
            .Include(u => u.AssignedCategories)
            .FirstOrDefaultAsync(u => u.Id == id && u.AcademyId == academyId);
        if (user == null) throw new Exception("Usuario no encontrado.");
        // Apoderados sin correo: si el formulario lo deja vacío, se conserva su correo interno.
        if (string.IsNullOrWhiteSpace(dto.Email) && StudentService.IsPlaceholderEmail(user.Email)) dto.Email = user.Email;
        if (string.IsNullOrWhiteSpace(dto.Email)) throw new Exception("El correo es obligatorio.");
        if (await EmailInUseAsync(dto.Email, user.Id))
            throw new Exception("El correo ya está en uso por otro usuario.");

        var sysRole = ParseAssignableRole(dto.SystemRole, user.Role);
        await ValidateUserReferencesAsync(academyId, dto.AcademyRoleId, dto.HeadquarterId);

        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.Email = dto.Email.Trim().ToLower();
        if (!string.IsNullOrEmpty(dto.Password))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            user.MustChangePassword = true;
        }
        user.Phone = dto.Phone;
        user.Role = sysRole;
        user.AcademyRoleId = dto.AcademyRoleId;
        user.HeadquarterId = dto.HeadquarterId;
        user.PaymentType = (StaffPaymentType)dto.PaymentType;
        user.PaymentRate = dto.PaymentRate;
        if (Enum.TryParse<UserStatus>(dto.Status, out var newStatus))
            user.Status = newStatus;

        if (dto.CategoryIds != null)
        {
            var categories = await _context.Categories
                .Where(c => c.AcademyId == academyId && dto.CategoryIds.Contains(c.Id))
                .ToListAsync();
            user.AssignedCategories.Clear();
            foreach (var cat in categories) user.AssignedCategories.Add(cat);
        }
        else
        {
            user.AssignedCategories.Clear();
        }

        await _context.SaveChangesAsync();
        return new AcademyUserDto { Id = user.Id, FirstName = user.FirstName, LastName = user.LastName, Email = user.Email };
    }

    /// <summary>Genera una contraseña temporal aleatoria, obliga a cambiarla y la devuelve para entregarla al usuario.</summary>
    public async Task<string> ResetUserPasswordAsync(Guid academyId, Guid id)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.AcademyId == academyId);
        if (user == null) throw new Exception("Usuario no encontrado.");

        var temp = AuthService.AssignTemporaryPassword(user);
        _audit.Add(academyId, "users.password.reset", "User", user.Id, new { user.Email });
        await _context.SaveChangesAsync();
        return temp;
    }
}
