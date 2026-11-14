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
    public List<Guid>? CategoryIds { get; set; } = new List<Guid>();
    public Guid? TeacherId { get; set; } // Referencia a un User (Role=Staff)
    
    // Si es tipo TournamentMatch
    public Guid? TournamentId { get; set; }
    /// <summary>Equipo de la academia que juega el partido de torneo: define plantel convocado y responsable.</summary>
    public Guid? TournamentTeamId { get; set; }
    /// <summary>Fase o fecha del torneo ("Fecha 3", "Semifinal").</summary>
    public string? Round { get; set; }
    /// <summary>Hora de citación (hora de pared, como StartTime). Null = misma hora del partido.</summary>
    public DateTime? CallTime { get; set; }
    /// <summary>Cancha o lugar del partido cuando no es una sede de la academia.</summary>
    public string? Location { get; set; }
    /// <summary>Estado del partido (amistoso o torneo). Null en eventos que no son partidos.</summary>
    public MatchStatus? MatchStatus { get; set; }

    // Si es tipo FriendlyMatch u otro
    public string? OpponentTeam { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Serie de horario recurrente a la que pertenece (null = evento suelto).</summary>
    public Guid? SeriesId { get; set; }

    // Resultado del partido (amistoso / torneo)
    public int? GoalsFor { get; set; }
    public int? GoalsAgainst { get; set; }
    public string? MatchNotes { get; set; }

    // Attendance closure — Coach/Admin can close; only Admin can reopen
    public bool AttendanceClosed { get; set; } = false;
    public DateTime? AttendanceClosedAt { get; set; }
    public Guid? AttendanceClosedBy { get; set; }
    /// <summary>Reapertura por un administrador: el cierre automático ya no vuelve a tocar esta lista.</summary>
    public DateTime? AttendanceReopenedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    /// <summary>Todas las categorías de la sesión, sin repetir.</summary>
    public List<Guid> AllCategoryIds()
    {
        var ids = new List<Guid>(CategoryIds ?? new List<Guid>());
        if (CategoryId.HasValue) ids.Add(CategoryId.Value);
        return ids.Distinct().ToList();
    }

    // Navigations
    public virtual Academy Academy { get; set; } = null!;
    public virtual Headquarter? Headquarter { get; set; }
    public virtual Category? Category { get; set; }
    public virtual User? Teacher { get; set; }
    public virtual Tournament? Tournament { get; set; }
    public virtual TournamentTeam? TournamentTeam { get; set; }
}
