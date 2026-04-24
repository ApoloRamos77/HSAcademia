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
    public int MinAge { get; set; }
    public int MaxAge { get; set; }
    public bool IsActive { get; set; }
}

public class CreateCategoryDto
{
    public Guid HeadquarterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinAge { get; set; }
    public int MaxAge { get; set; }
}

public class AcademyRoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateAcademyRoleDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AcademyUserDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string SystemRole { get; set; } = string.Empty;
    public Guid? AcademyRoleId { get; set; }
    public string? AcademyRoleName { get; set; }
    public Guid? HeadquarterId { get; set; }
    public string? HeadquarterName { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<Guid> CategoryIds { get; set; } = new List<Guid>();
    public List<string> CategoryNames { get; set; } = new List<string>();
}

public class CreateAcademyUserDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string SystemRole { get; set; } = "Staff"; // e.g. Staff, AcademyAdmin
    public Guid? AcademyRoleId { get; set; }
    public Guid? HeadquarterId { get; set; }
    public List<Guid>? CategoryIds { get; set; }
}
