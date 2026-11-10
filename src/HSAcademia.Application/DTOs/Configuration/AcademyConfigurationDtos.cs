using System;

namespace HSAcademia.Application.DTOs.Configuration;

public class HeadquarterDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; }
}

public class CreateHeadquarterDto
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ContactPhone { get; set; }
}

public class CategoryDto
{
    public Guid Id { get; set; }
    public Guid HeadquarterId { get; set; }
    public string HeadquarterName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime StartDateOfBirth { get; set; }
    public DateTime EndDateOfBirth { get; set; }
    public decimal MonthlyFee { get; set; }
    public bool IsActive { get; set; }
}

public class CreateCategoryDto
{
    public Guid HeadquarterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDateOfBirth { get; set; }
    public DateTime EndDateOfBirth { get; set; }
    /// <summary>Mensualidad de la categoría. En edición, null = conservar la actual.</summary>
    public decimal? MonthlyFee { get; set; }
}

public class AcademyRoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = new List<string>();
    public bool IsActive { get; set; }
    public bool IsSystem { get; set; }
    public int UserCount { get; set; }
}

public class SetAcademyRoleStatusDto
{
    public bool IsActive { get; set; }
}

public class CreateAcademyRoleDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = new List<string>();
}

public class AcademyUserDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string SystemRole { get; set; } = string.Empty;
    public bool IsOwner { get; set; }
    public List<Guid> AcademyRoleIds { get; set; } = new List<Guid>();
    public List<string> AcademyRoleNames { get; set; } = new List<string>();
    public Guid? HeadquarterId { get; set; }
    public string? HeadquarterName { get; set; }
    public List<Guid> HeadquarterIds { get; set; } = new List<Guid>();
    public List<string> HeadquarterNames { get; set; } = new List<string>();
    public string Status { get; set; } = string.Empty;
    public List<Guid> CategoryIds { get; set; } = new List<Guid>();
    public List<string> CategoryNames { get; set; } = new List<string>();
    public int PaymentType { get; set; }
    public decimal PaymentRate { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool MustChangePassword { get; set; }
    public string? SuspensionReason { get; set; }
    /// <summary>Solo al crear sin contraseña: se muestra una única vez.</summary>
    public string? TemporaryPassword { get; set; }
}

public class CreateAcademyUserDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string SystemRole { get; set; } = "Staff"; // e.g. Staff, AcademyAdmin
    /// <summary>Roles asignados. Si no se envía, se conservan los actuales.</summary>
    public List<Guid>? AcademyRoleIds { get; set; }
    public Guid? HeadquarterId { get; set; }
    /// <summary>Sedes asignadas; vacío = todas. Si no se envía, se usa HeadquarterId.</summary>
    public List<Guid>? HeadquarterIds { get; set; }
    public List<Guid>? CategoryIds { get; set; }
    /// <summary>Se gestionan en la ficha laboral; si no se envían, se conservan.</summary>
    public int? PaymentType { get; set; } // 0: Monthly, 1: PerSession
    public decimal? PaymentRate { get; set; }
    public string Status { get; set; } = "Active";
    public string? SuspensionReason { get; set; }
}

public class UpdateEmploymentDto
{
    public int PaymentType { get; set; }
    public decimal PaymentRate { get; set; }
}

public class ActivityItemDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string? TargetName { get; set; }
    public string? IpAddress { get; set; }
    public string Device { get; set; } = string.Empty;
    public System.Text.Json.JsonElement? Details { get; set; }
}

public class ActivityPageDto
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ActivityItemDto> Items { get; set; } = new();
}
