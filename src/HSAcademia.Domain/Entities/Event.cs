using HSAcademia.Domain.Enums;

namespace HSAcademia.Domain.Entities;

public class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; } // Tenant
    
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EventType Type { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public Guid? HeadquarterId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? TeacherId { get; set; } // Referencia a un User (Role=Staff)
    
    // Si es tipo TournamentMatch
    public Guid? TournamentId { get; set; }

    // Si es tipo FriendlyMatch u otro
    public string? OpponentTeam { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigations
    public virtual Academy Academy { get; set; } = null!;
    public virtual Headquarter? Headquarter { get; set; }
    public virtual Category? Category { get; set; }
    public virtual User? Teacher { get; set; }
    public virtual Tournament? Tournament { get; set; }
}
