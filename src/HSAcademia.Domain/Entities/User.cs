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
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.AcademyAdmin;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public string? SuspensionReason { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpiry { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? BirthDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public Academy? Academy { get; set; }
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public Guid? AcademyRoleId { get; set; }
    public AcademyRole? AcademyRole { get; set; }
    
    public Guid? HeadquarterId { get; set; }
    public Headquarter? Headquarter { get; set; }

    public ICollection<Category> AssignedCategories { get; set; } = new List<Category>();
}
