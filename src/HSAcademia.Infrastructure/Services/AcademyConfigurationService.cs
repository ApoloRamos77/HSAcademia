using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Configuration;
using HSAcademia.Domain.Authorization;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

public class AcademyConfigurationService
{
    private readonly AppDbContext _context;
    private readonly AuditService _audit;
    private readonly AccessScopeService _scope;

    public AcademyConfigurationService(AppDbContext context, AuditService audit, AccessScopeService scope)
    {
        _context = context;
        _scope = scope;
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
            var scope = await _scope.ForUserAsync(userId.Value);
            if (scope.Categories.Count > 0)
            {
                var assigned = scope.Categories;
                query = query.Where(c => assigned.Contains(c.Id));
            }
            else if (!scope.SeesWholeSede)
            {
                return new List<CategoryDto>();
            }
            else if (scope.Headquarters != null)
            {
                var headquarters = scope.Headquarters;
                query = query.Where(c => headquarters.Contains(c.HeadquarterId));
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
        await EnsureSystemRolesAsync(academyId);
        return await _context.AcademyRoles
            .Where(r => r.AcademyId == academyId)
            .OrderBy(r => r.SystemKey == null).ThenBy(r => r.Name)
            .Select(r => new AcademyRoleDto
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                Permissions = r.Permissions,
                IsActive = r.IsActive,
                IsSystem = r.SystemKey != null,
                UserCount = r.Users.Count(u => !u.IsDeleted)
            })
            .ToListAsync();
    }

    /// <summary>Crea o actualiza los roles predefinidos de la academia; como no se editan, siempre siguen a Clubio.</summary>
    private async Task EnsureSystemRolesAsync(Guid academyId)
    {
        var existing = await _context.AcademyRoles
            .Where(r => r.AcademyId == academyId && r.SystemKey != null)
            .ToListAsync();
        var changed = false;
        foreach (var template in SystemRoles.All)
        {
            var role = existing.FirstOrDefault(r => r.SystemKey == template.Key);
            if (role == null)
            {
                // Dos roles con el mismo nombre confunden al asignar: el propio de la academia se renombra.
                var namesake = await _context.AcademyRoles.FirstOrDefaultAsync(r =>
                    r.AcademyId == academyId && r.SystemKey == null && r.Name.ToLower() == template.Name.ToLower());
                if (namesake != null) namesake.Name = $"{template.Name} (propio)";
                _context.AcademyRoles.Add(new AcademyRole
                {
                    AcademyId = academyId,
                    SystemKey = template.Key,
                    Name = template.Name,
                    Description = template.Description,
                    Permissions = template.Permissions.ToList()
                });
                changed = true;
            }
            else if (role.Name != template.Name || role.Description != template.Description
                     || !role.Permissions.ToHashSet().SetEquals(template.Permissions))
            {
                role.Name = template.Name;
                role.Description = template.Description;
                role.Permissions = template.Permissions.ToList();
                changed = true;
            }
        }
        if (!changed) return;

        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateException) { _context.ChangeTracker.Clear(); } // otra petición simultánea ya los creó
    }

    public async Task<AcademyRoleDto> CreateRoleAsync(Guid academyId, CreateAcademyRoleDto dto)
    {
        var (name, permissions) = await ValidateRoleAsync(academyId, dto, null);
        var role = new AcademyRole { AcademyId = academyId, Name = name, Description = dto.Description, Permissions = permissions };
        _context.AcademyRoles.Add(role);
        _audit.Add(academyId, "roles.created", nameof(AcademyRole), role.Id, new { role.Name, role.Description, role.Permissions });
        await _context.SaveChangesAsync();
        return ToRoleDto(role);
    }

    public async Task<AcademyRoleDto> UpdateRoleAsync(Guid academyId, Guid id, CreateAcademyRoleDto dto)
    {
        var role = await _context.AcademyRoles.FirstOrDefaultAsync(r => r.Id == id && r.AcademyId == academyId);
        if (role == null) throw new Exception("Rol no encontrado.");
        EnsureEditable(role);
        var (name, permissions) = await ValidateRoleAsync(academyId, dto, role);

        var before = new { role.Name, role.Description, Permissions = role.Permissions.ToList() };
        var changed = before.Name != name || before.Description != dto.Description || !before.Permissions.ToHashSet().SetEquals(permissions);

        role.Name = name;
        role.Description = dto.Description;
        role.Permissions = permissions;
        if (changed)
            _audit.Add(academyId, "roles.updated", nameof(AcademyRole), role.Id, new { before, after = new { role.Name, role.Description, role.Permissions } });
        await _context.SaveChangesAsync();
        return ToRoleDto(role);
    }

    public async Task<AcademyRoleDto> CloneRoleAsync(Guid academyId, Guid id)
    {
        var source = await _context.AcademyRoles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.AcademyId == academyId)
            ?? throw new Exception("Rol no encontrado.");
        var names = await _context.AcademyRoles.Where(r => r.AcademyId == academyId).Select(r => r.Name.ToLower()).ToListAsync();
        var name = $"Copia de {source.Name}";
        for (var n = 2; names.Contains(name.ToLower()); n++) name = $"Copia de {source.Name} ({n})";

        var role = new AcademyRole { AcademyId = academyId, Name = name, Description = source.Description, Permissions = source.Permissions.ToList() };
        _context.AcademyRoles.Add(role);
        _audit.Add(academyId, "roles.created", nameof(AcademyRole), role.Id, new { role.Name, role.Permissions, clonedFrom = source.Name });
        await _context.SaveChangesAsync();
        return ToRoleDto(role);
    }

    public async Task<AcademyRoleDto> SetRoleStatusAsync(Guid academyId, Guid id, bool isActive)
    {
        var role = await _context.AcademyRoles.FirstOrDefaultAsync(r => r.Id == id && r.AcademyId == academyId)
            ?? throw new Exception("Rol no encontrado.");
        EnsureEditable(role);
        if (role.IsActive != isActive)
        {
            role.IsActive = isActive;
            _audit.Add(academyId, isActive ? "roles.activated" : "roles.deactivated", nameof(AcademyRole), role.Id, new { role.Name });
            await _context.SaveChangesAsync();
        }
        return ToRoleDto(role);
    }

    public async Task DeleteRoleAsync(Guid academyId, Guid id)
    {
        var role = await _context.AcademyRoles.FirstOrDefaultAsync(r => r.Id == id && r.AcademyId == academyId)
            ?? throw new Exception("Rol no encontrado.");
        EnsureEditable(role);
        if (await _context.AcademyRoles.Where(r => r.Id == id).AnyAsync(r => r.Users.Any()))
            throw new Exception("El rol tiene personal asignado. Reasígnalo antes de eliminarlo, o desactívalo.");
        role.IsDeleted = true;
        role.DeletedAt = DateTime.UtcNow;
        _audit.Add(academyId, "roles.deleted", nameof(AcademyRole), role.Id, new { role.Name });
        await _context.SaveChangesAsync();
    }

    private static void EnsureEditable(AcademyRole role)
    {
        if (role.SystemKey != null)
            throw new Exception("Los roles predefinidos no se modifican. Clónalo para adaptarlo.");
    }

    private static AcademyRoleDto ToRoleDto(AcademyRole role) =>
        new() { Id = role.Id, Name = role.Name, Description = role.Description, Permissions = role.Permissions, IsActive = role.IsActive, IsSystem = role.SystemKey != null };

    private async Task<(string Name, List<string> Permissions)> ValidateRoleAsync(Guid academyId, CreateAcademyRoleDto dto, AcademyRole? existing)
    {
        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new Exception("El nombre del cargo es obligatorio.");

        if (existing == null || !string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            var normalized = name.ToLower();
            if (await _context.AcademyRoles.AnyAsync(r => r.AcademyId == academyId && r.Name.ToLower() == normalized))
                throw new Exception("Ya existe un cargo con ese nombre.");
        }

        var permissions = (dto.Permissions ?? new List<string>()).Select(p => p.Trim()).Distinct().ToList();
        var unknown = permissions.Except(Permissions.All).ToList();
        if (unknown.Count > 0) throw new Exception($"Permisos no válidos: {string.Join(", ", unknown)}.");
        return (name, permissions);
    }

    // ==========================================
    // Academy Users (Usuarios y roles)
    // ==========================================
    public async Task<List<AcademyUserDto>> GetUsersAsync(Guid academyId, bool staffOnly = false)
    {
        return await _context.Users
            .Include(u => u.AssignedRoles)
            .Include(u => u.Headquarter)
            .Include(u => u.AssignedCategories)
            .Include(u => u.AssignedHeadquarters)
            .Where(u => u.AcademyId == academyId && (!staffOnly || u.Role == UserRole.Staff || u.Role == UserRole.AcademyAdmin))
            .Select(u => new AcademyUserDto
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = StudentService.IsPlaceholderEmail(u.Email) ? "" : u.Email,
                Phone = u.Phone,
                SystemRole = u.Role.ToString(),
                IsOwner = u.IsOwner,
                AcademyRoleIds = u.AssignedRoles.Select(r => r.Id).ToList(),
                AcademyRoleNames = u.AssignedRoles.Select(r => r.Name).ToList(),
                HeadquarterId = u.HeadquarterId,
                HeadquarterName = u.Headquarter != null ? u.Headquarter.Name : null,
                HeadquarterIds = u.AssignedHeadquarters.Select(h => h.Id).ToList(),
                HeadquarterNames = u.AssignedHeadquarters.Select(h => h.Name).ToList(),
                Status = u.Status.ToString(),
                CategoryIds = u.AssignedCategories.Select(c => c.Id).ToList(),
                CategoryNames = u.AssignedCategories.Select(c => c.Name).ToList(),
                PaymentType = (int)u.PaymentType,
                PaymentRate = u.PaymentRate,
                LastLoginAt = u.LastLoginAt,
                MustChangePassword = u.MustChangePassword,
                SuspensionReason = u.SuspensionReason
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

    private static List<Guid> RequestedHeadquarters(CreateAcademyUserDto dto) =>
        (dto.HeadquarterIds ?? (dto.HeadquarterId.HasValue ? new List<Guid> { dto.HeadquarterId.Value } : new List<Guid>()))
            .Distinct().ToList();

    private async Task<(List<AcademyRole> Roles, List<Headquarter> Headquarters)> ResolveUserReferencesAsync(Guid academyId, List<Guid> roleIds, List<Guid> headquarterIds)
    {
        var roles = await _context.AcademyRoles
            .Where(r => r.AcademyId == academyId && roleIds.Contains(r.Id))
            .ToListAsync();
        if (roles.Count != roleIds.Count) throw new Exception("El cargo seleccionado no es válido.");

        var headquarters = await _context.Headquarters
            .Where(h => h.AcademyId == academyId && headquarterIds.Contains(h.Id))
            .ToListAsync();
        if (headquarters.Count != headquarterIds.Count) throw new Exception("La sede seleccionada no es válida.");
        return (roles, headquarters);
    }

    /// <summary>Solo el propietario gestiona a los administradores; cada uno conserva su propia ficha.</summary>
    private async Task EnsureCanManageAsync(Guid academyId, Guid actorUserId, User? target, UserRole newRole)
    {
        if (newRole != UserRole.AcademyAdmin && target?.Role != UserRole.AcademyAdmin) return;
        if (target?.Id == actorUserId) return;
        if (!await _context.Users.AnyAsync(u => u.Id == actorUserId && u.AcademyId == academyId && u.IsOwner))
            throw new Exception("Solo el propietario de la academia gestiona a los administradores.");
    }

    public async Task TransferOwnershipAsync(Guid academyId, Guid actorUserId, Guid targetUserId)
    {
        var owner = await _context.Users.FirstOrDefaultAsync(u => u.Id == actorUserId && u.AcademyId == academyId && u.IsOwner)
            ?? throw new Exception("Solo el propietario puede transferir la academia.");
        var target = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId && u.AcademyId == academyId)
            ?? throw new Exception("Usuario no encontrado.");
        if (target.Id == owner.Id) throw new Exception("Ya eres el propietario.");
        if (target.Role != UserRole.AcademyAdmin || target.Status != UserStatus.Active)
            throw new Exception("El nuevo propietario debe ser un administrador activo.");

        // Dos pasos: el índice único admite un solo propietario en todo momento.
        await _context.InTransactionAsync(async () =>
        {
            owner.IsOwner = false;
            await _context.SaveChangesAsync();
            target.IsOwner = true;
            _audit.Add(academyId, "users.ownership.transferred", nameof(User), target.Id, new { from = owner.Email, to = target.Email });
            await _context.SaveChangesAsync();
            return true;
        });
    }

    /// <summary>Datos de acceso legibles para la bitácora. Nunca incluye contraseñas.</summary>
    private static Dictionary<string, string?> AccessSnapshot(User user) => new()
    {
        ["nombre"] = $"{user.FirstName} {user.LastName}",
        ["correo"] = user.Email,
        ["telefono"] = user.Phone,
        ["nivelAcceso"] = user.Role.ToString(),
        ["roles"] = string.Join(", ", user.AssignedRoles.Select(r => r.Name).OrderBy(n => n)),
        ["estado"] = user.Status.ToString(),
        ["motivoSuspension"] = user.SuspensionReason,
        ["sedes"] = string.Join(", ", user.AssignedHeadquarters.Select(h => h.Name).OrderBy(n => n)),
        ["categorias"] = string.Join(", ", user.AssignedCategories.Select(c => c.Name).OrderBy(n => n)),
        ["tipoPago"] = user.PaymentType.ToString(),
        ["tarifa"] = user.PaymentRate.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
    };

    public async Task<AcademyUserDto> CreateUserAsync(Guid academyId, CreateAcademyUserDto dto, Guid actorUserId)
    {
        if (string.IsNullOrWhiteSpace(dto.Email)) throw new Exception("El correo es obligatorio.");
        if (await EmailInUseAsync(dto.Email))
            throw new Exception("El correo ya está en uso.");

        var sysRole = ParseAssignableRole(dto.SystemRole, UserRole.Staff);
        var requestedHeadquarters = RequestedHeadquarters(dto);
        await EnsureCanManageAsync(academyId, actorUserId, null, sysRole);
        var (roles, headquarters) = await ResolveUserReferencesAsync(academyId, (dto.AcademyRoleIds ?? new List<Guid>()).Distinct().ToList(), requestedHeadquarters);

        // Sin contraseña indicada se genera una temporal segura, que se muestra una sola vez.
        var temporaryPassword = string.IsNullOrWhiteSpace(dto.Password) ? PasswordPolicy.GenerateTemporary() : null;
        var initialPassword = temporaryPassword ?? dto.Password;
        if (initialPassword.Length < 6)
            throw new Exception("La contraseña inicial debe tener al menos 6 caracteres.");

        var user = new User
        {
            AcademyId = academyId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email.Trim().ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(initialPassword),
            // La contraseña la definió el administrador: el usuario debe cambiarla al ingresar.
            MustChangePassword = true,
            Phone = dto.Phone,
            Role = sysRole,
            AssignedRoles = roles,
            HeadquarterId = requestedHeadquarters.Count == 1 ? requestedHeadquarters[0] : null,
            AssignedHeadquarters = headquarters,
            Status = UserStatus.Active,
            PaymentType = (StaffPaymentType)(dto.PaymentType ?? 0),
            PaymentRate = dto.PaymentRate ?? 0m
        };

        if (dto.CategoryIds != null && dto.CategoryIds.Any())
        {
            var categories = await _context.Categories
                .Where(c => c.AcademyId == academyId && dto.CategoryIds.Contains(c.Id))
                .ToListAsync();
            user.AssignedCategories = categories;
        }

        _context.Users.Add(user);
        _audit.Add(academyId, "users.created", nameof(User), user.Id, AccessSnapshot(user));
        await _context.SaveChangesAsync();

        return new AcademyUserDto { Id = user.Id, FirstName = user.FirstName, LastName = user.LastName, Email = user.Email, TemporaryPassword = temporaryPassword };
    }

    public async Task<AcademyUserDto> UpdateUserAsync(Guid academyId, Guid id, CreateAcademyUserDto dto, Guid actorUserId)
    {
        var user = await _context.Users
            .Include(u => u.AssignedCategories)
            .Include(u => u.AssignedHeadquarters)
            .Include(u => u.AssignedRoles)
            .FirstOrDefaultAsync(u => u.Id == id && u.AcademyId == academyId);
        if (user == null) throw new Exception("Usuario no encontrado.");
        // Apoderados sin correo: si el formulario lo deja vacío, se conserva su correo interno.
        if (string.IsNullOrWhiteSpace(dto.Email) && StudentService.IsPlaceholderEmail(user.Email)) dto.Email = user.Email;
        if (string.IsNullOrWhiteSpace(dto.Email)) throw new Exception("El correo es obligatorio.");
        if (await EmailInUseAsync(dto.Email, user.Id))
            throw new Exception("El correo ya está en uso por otro usuario.");

        var sysRole = ParseAssignableRole(dto.SystemRole, user.Role);
        var requestedHeadquarters = RequestedHeadquarters(dto);
        await EnsureCanManageAsync(academyId, actorUserId, user, sysRole);
        var requestedRoles = dto.AcademyRoleIds?.Distinct().ToList() ?? user.AssignedRoles.Select(r => r.Id).ToList();
        var (roles, headquarters) = await ResolveUserReferencesAsync(academyId, requestedRoles, requestedHeadquarters);
        var newStatus = Enum.TryParse<UserStatus>(dto.Status, out var parsedStatus) ? parsedStatus : user.Status;

        // Quien administra no puede dejarse fuera: otro administrador debe hacer el cambio, y así
        // la academia nunca se queda sin un administrador activo.
        if (user.Id == actorUserId && (sysRole != user.Role || newStatus != user.Status))
            throw new Exception("No puedes cambiar tu propio nivel de acceso ni tu estado. Pide a otro administrador que lo haga.");

        var before = AccessSnapshot(user);
        var passwordChanged = !string.IsNullOrEmpty(dto.Password);

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
        user.AssignedRoles.Clear();
        foreach (var role in roles) user.AssignedRoles.Add(role);
        user.HeadquarterId = requestedHeadquarters.Count == 1 ? requestedHeadquarters[0] : null;
        user.AssignedHeadquarters.Clear();
        foreach (var headquarter in headquarters) user.AssignedHeadquarters.Add(headquarter);
        if (dto.PaymentType.HasValue) user.PaymentType = (StaffPaymentType)dto.PaymentType.Value;
        if (dto.PaymentRate.HasValue) user.PaymentRate = dto.PaymentRate.Value;
        if (newStatus == UserStatus.Suspended)
        {
            if (user.Status != UserStatus.Suspended) user.SuspendedAt = DateTime.UtcNow;
            user.SuspensionReason = string.IsNullOrWhiteSpace(dto.SuspensionReason) ? user.SuspensionReason : dto.SuspensionReason.Trim();
        }
        else
        {
            user.SuspendedAt = null;
            user.SuspensionReason = null;
        }
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

        var after = AccessSnapshot(user);
        var changes = after.Where(kv => before[kv.Key] != kv.Value)
            .ToDictionary(kv => kv.Key, kv => new { from = before[kv.Key], to = kv.Value });
        if (changes.Count > 0 || passwordChanged)
            _audit.Add(academyId, "users.updated", nameof(User), user.Id, new { changes, passwordChanged });

        await _context.SaveChangesAsync();
        return new AcademyUserDto { Id = user.Id, FirstName = user.FirstName, LastName = user.LastName, Email = user.Email };
    }

    /// <summary>Sueldo y tipo de pago: datos laborales, independientes de los accesos.</summary>
    public async Task UpdateEmploymentAsync(Guid academyId, Guid id, UpdateEmploymentDto dto)
    {
        if (!Enum.IsDefined(typeof(StaffPaymentType), dto.PaymentType)) throw new Exception("Tipo de pago no válido.");
        if (dto.PaymentRate < 0) throw new Exception("La tarifa no puede ser negativa.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.AcademyId == academyId
                && (u.Role == UserRole.Staff || u.Role == UserRole.AcademyAdmin))
            ?? throw new Exception("Usuario no encontrado.");

        var type = (StaffPaymentType)dto.PaymentType;
        if (user.PaymentType == type && user.PaymentRate == dto.PaymentRate) return;
        _audit.Add(academyId, "users.employment.updated", nameof(User), user.Id, new
        {
            changes = new
            {
                tipoPago = new { from = user.PaymentType.ToString(), to = type.ToString() },
                tarifa = new { from = user.PaymentRate, to = dto.PaymentRate }
            }
        });
        user.PaymentType = type;
        user.PaymentRate = dto.PaymentRate;
        await _context.SaveChangesAsync();
    }

    /// <summary>Genera una contraseña temporal aleatoria, obliga a cambiarla y la devuelve para entregarla al usuario.</summary>
    public async Task<string> ResetUserPasswordAsync(Guid academyId, Guid id, Guid actorUserId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.AcademyId == academyId);
        if (user == null) throw new Exception("Usuario no encontrado.");
        await EnsureCanManageAsync(academyId, actorUserId, user, user.Role);

        var temp = AuthService.AssignTemporaryPassword(user);
        _audit.Add(academyId, "users.password.reset", "User", user.Id, new { user.Email });
        await _context.SaveChangesAsync();
        return temp;
    }
}
