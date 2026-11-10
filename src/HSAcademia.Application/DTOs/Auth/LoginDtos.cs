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

public class ChangePasswordRequestDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
