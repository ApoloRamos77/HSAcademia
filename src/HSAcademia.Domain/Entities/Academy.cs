using HSAcademia.Domain.Enums;

namespace HSAcademia.Domain.Entities;

public class Academy
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Tenant discriminator - used in all multi-tenant queries
    public Guid AcademyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string SlugName { get; set; } = string.Empty;        // URL-safe identifier
    public string? Description { get; set; }
    public string ContactEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string? LogoUrl { get; set; }
    public string? Address { get; set; }

    // Datos fijos de los comprobantes: no dependen de la sede ni de dónde se cobre.
    public string? ReceiptAddress { get; set; }
    public string? ReceiptPhone { get; set; }

    // Avisos automáticos (push)
    /// <summary>Recordatorio de encuentros a las familias de los convocados y al responsable.</summary>
    public bool NotifyMatchEnabled { get; set; } = true;
    /// <summary>Horas antes del encuentro (1–24).</summary>
    public int NotifyMatchHoursBefore { get; set; } = 2;
    /// <summary>Hora del aviso cuando el encuentro de hoy aún no tiene hora confirmada (5–20).</summary>
    public int NotifyMatchNoTimeHour { get; set; } = 7;
    /// <summary>Saludo de cumpleaños a la familia del alumno.</summary>
    public bool NotifyBirthdayEnabled { get; set; } = true;
    /// <summary>Hora del saludo de cumpleaños (5–20).</summary>
    public int NotifyBirthdayHour { get; set; } = 8;
    /// <summary>Aviso de cumpleaños al personal (entrenadores de su categoría, coordinadores y administradores).</summary>
    public bool NotifyBirthdayStaff { get; set; } = true;
    /// <summary>Saludo propio; null = el de Clubio. Admite {nombre}, {academia} y {edad}.</summary>
    public string? BirthdayMessage { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Website { get; set; }
    public string? Sport { get; set; }                          // Main sport discipline
    public AcademyStatus Status { get; set; } = AcademyStatus.Pending;
    public string? RejectionReason { get; set; }
    public string? SuspensionReason { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<AcademyRegistrationRequest> RegistrationRequests { get; set; } = new List<AcademyRegistrationRequest>();
    public ICollection<Headquarter> Headquarters { get; set; } = new List<Headquarter>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<AcademyRole> Roles { get; set; } = new List<AcademyRole>();
}
