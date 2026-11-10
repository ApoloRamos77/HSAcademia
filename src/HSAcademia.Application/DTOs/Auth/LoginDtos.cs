namespace HSAcademia.Application.DTOs.Auth;

public class LoginRequestDto
{
    public string EmailOrPhone { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Role { get; set; }
    /// <summary>Opcional: academia seleccionada (acepta el Id o el TenantId devuelto por /auth/academies).
    /// Necesario cuando el mismo teléfono/DNI existe en varias academias.</summary>
    public Guid? AcademyId { get; set; }
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid? AcademyId { get; set; }
    public string? AcademyName { get; set; }
    public bool RequirePasswordChange { get; set; }
    public Guid? CategoryId { get; set; } // For Student/Guardian: their category
    public List<string> Permissions { get; set; } = new List<string>();
    public bool IsOwner { get; set; }
    /// <summary>False si el usuario está limitado a ciertas sedes.</summary>
    public bool AcademyWide { get; set; } = true;
}

public class ProfilePermissionGroupDto
{
    public string Name { get; set; } = string.Empty;
    public List<string> Items { get; set; } = new();
}

public class ProfileLoginDto
{
    public DateTime At { get; set; }
    public string Device { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
}

public class ProfileDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? DocumentNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsOwner { get; set; }
    public string? AcademyName { get; set; }
    /// <summary>Administrador o propietario: no depende de roles.</summary>
    public bool FullAccess { get; set; }
    public List<string> Roles { get; set; } = new();
    /// <summary>Vacío = todas las sedes.</summary>
    public List<string> Headquarters { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public List<ProfilePermissionGroupDto> PermissionGroups { get; set; } = new();
    public List<ProfileLoginDto> RecentLogins { get; set; } = new();
}

public class UpdateProfileDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

public class ChangePasswordRequestDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
