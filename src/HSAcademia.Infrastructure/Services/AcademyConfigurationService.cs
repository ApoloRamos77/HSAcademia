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

    public AcademyConfigurationService(AppDbContext context)
    {
        _context = context;
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
    public async Task<List<CategoryDto>> GetCategoriesAsync(Guid academyId)
    {
        return await _context.Categories
            .Include(c => c.Headquarter)
            .Where(c => c.AcademyId == academyId)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                HeadquarterId = c.HeadquarterId,
                HeadquarterName = c.Headquarter!.Name,
                Name = c.Name,
                MinAge = c.MinAge,
                MaxAge = c.MaxAge,
                IsActive = c.IsActive
            })
            .ToListAsync();
    }

    public async Task<CategoryDto> CreateCategoryAsync(Guid academyId, CreateCategoryDto dto)
    {
        var hq = await _context.Headquarters.FirstOrDefaultAsync(h => h.Id == dto.HeadquarterId && h.AcademyId == academyId);
        if (hq == null) throw new Exception("Sede no encontrada.");

        var category = new Category
        {
            AcademyId = academyId,
            HeadquarterId = dto.HeadquarterId,
            Name = dto.Name,
            MinAge = dto.MinAge,
            MaxAge = dto.MaxAge
        };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        return new CategoryDto { Id = category.Id, HeadquarterId = category.HeadquarterId, HeadquarterName = hq.Name, Name = category.Name, MinAge = category.MinAge, MaxAge = category.MaxAge, IsActive = category.IsActive };
    }

    public async Task<CategoryDto> UpdateCategoryAsync(Guid academyId, Guid id, CreateCategoryDto dto)
    {
        var category = await _context.Categories.Include(c => c.Headquarter).FirstOrDefaultAsync(c => c.Id == id && c.AcademyId == academyId);
        if (category == null) throw new Exception("Categoría no encontrada.");
        var hq = await _context.Headquarters.FirstOrDefaultAsync(h => h.Id == dto.HeadquarterId && h.AcademyId == academyId);
        if (hq == null) throw new Exception("Sede no encontrada.");

        category.HeadquarterId = dto.HeadquarterId;
        category.Name = dto.Name;
        category.MinAge = dto.MinAge;
        category.MaxAge = dto.MaxAge;
        await _context.SaveChangesAsync();
        return new CategoryDto { Id = category.Id, HeadquarterId = category.HeadquarterId, HeadquarterName = hq.Name, Name = category.Name, MinAge = category.MinAge, MaxAge = category.MaxAge, IsActive = category.IsActive };
    }

    // ==========================================
    // Academy Roles (Roles personalizados)
    // ==========================================
    public async Task<List<AcademyRoleDto>> GetRolesAsync(Guid academyId)
    {
        return await _context.AcademyRoles
            .Where(r => r.AcademyId == academyId)
            .Select(r => new AcademyRoleDto { Id = r.Id, Name = r.Name, Description = r.Description, IsActive = r.IsActive })
            .ToListAsync();
    }

    public async Task<AcademyRoleDto> CreateRoleAsync(Guid academyId, CreateAcademyRoleDto dto)
    {
        var role = new AcademyRole { AcademyId = academyId, Name = dto.Name, Description = dto.Description };
        _context.AcademyRoles.Add(role);
        await _context.SaveChangesAsync();
        return new AcademyRoleDto { Id = role.Id, Name = role.Name, Description = role.Description, IsActive = role.IsActive };
    }

    public async Task<AcademyRoleDto> UpdateRoleAsync(Guid academyId, Guid id, CreateAcademyRoleDto dto)
    {
        var role = await _context.AcademyRoles.FirstOrDefaultAsync(r => r.Id == id && r.AcademyId == academyId);
        if (role == null) throw new Exception("Rol no encontrado.");
        role.Name = dto.Name;
        role.Description = dto.Description;
        await _context.SaveChangesAsync();
        return new AcademyRoleDto { Id = role.Id, Name = role.Name, Description = role.Description, IsActive = role.IsActive };
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
                Email = u.Email,
                Phone = u.Phone,
                SystemRole = u.Role.ToString(),
                AcademyRoleId = u.AcademyRoleId,
                AcademyRoleName = u.AcademyRole != null ? u.AcademyRole.Name : null,
                HeadquarterId = u.HeadquarterId,
                HeadquarterName = u.Headquarter != null ? u.Headquarter.Name : null,
                Status = u.Status.ToString(),
                CategoryIds = u.AssignedCategories.Select(c => c.Id).ToList(),
                CategoryNames = u.AssignedCategories.Select(c => c.Name).ToList()
            })
            .ToListAsync();
    }

    public async Task<AcademyUserDto> CreateUserAsync(Guid academyId, CreateAcademyUserDto dto)
    {
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            throw new Exception("El correo ya está en uso.");

        if (!Enum.TryParse<UserRole>(dto.SystemRole, out var sysRole)) sysRole = UserRole.Staff;

        var user = new User
        {
            AcademyId = academyId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Phone = dto.Phone,
            Role = sysRole,
            AcademyRoleId = dto.AcademyRoleId,
            HeadquarterId = dto.HeadquarterId,
            Status = UserStatus.Active
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
        if (user.Email != dto.Email && await _context.Users.AnyAsync(u => u.Email == dto.Email))
            throw new Exception("El correo ya está en uso por otro usuario.");

        if (!Enum.TryParse<UserRole>(dto.SystemRole, out var sysRole)) sysRole = UserRole.Staff;

        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.Email = dto.Email;
        if (!string.IsNullOrEmpty(dto.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        user.Phone = dto.Phone;
        user.Role = sysRole;
        user.AcademyRoleId = dto.AcademyRoleId;
        user.HeadquarterId = dto.HeadquarterId;

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

    public async Task<bool> ResetUserPasswordAsync(Guid academyId, Guid id, string newPassword = "123456")
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.AcademyId == academyId);
        if (user == null) throw new Exception("Usuario no encontrado.");
        
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        
        await _context.SaveChangesAsync();
        return true;
    }
}
