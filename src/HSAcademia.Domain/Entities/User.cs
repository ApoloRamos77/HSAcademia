using HSAcademia.Domain.Enums;

namespace HSAcademia.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Multi-tenant discriminator (null = SuperAdmin / platform-level user)
    public Guid? AcademyId { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? DocumentNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.AcademyAdmin;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public string? SuspensionReason { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpiry { get; set; }
    /// <summary>Obliga a cambiar la contraseña en el próximo inicio de sesión (contraseñas temporales).</summary>
    public bool MustChangePassword { get; set; } = false;
    public DateTime? LastLoginAt { get; set; }
    public DateTime? BirthDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public Academy? Academy { get; set; }
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    /// <summary>Roles asignados: sus permisos se suman.</summary>
    public ICollection<AcademyRole> AssignedRoles { get; set; } = new List<AcademyRole>();

    /// <summary>Único por academia. Gestiona a los administradores y no puede ser retirado por ellos.</summary>
    public bool IsOwner { get; set; } = false;
    
    public Guid? HeadquarterId { get; set; }
    public Headquarter? Headquarter { get; set; }

    /// <summary>Sedes asignadas. Vacío = todas las sedes. HeadquarterId conserva la sede cuando es una sola.</summary>
    public ICollection<Headquarter> AssignedHeadquarters { get; set; } = new List<Headquarter>();

    public ICollection<Category> AssignedCategories { get; set; } = new List<Category>();

    // Payment configuration
    public StaffPaymentType PaymentType { get; set; } = StaffPaymentType.Monthly;
    public decimal PaymentRate { get; set; } = 0m;
    public PayFrequency PayFrequency { get; set; } = PayFrequency.Monthly;
    /// <summary>Tarifario propio (excepción); null = el predeterminado de la academia.</summary>
    public Guid? PayScaleId { get; set; }
}
