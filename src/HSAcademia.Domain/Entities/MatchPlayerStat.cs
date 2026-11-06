namespace HSAcademia.Domain.Entities;

/// <summary>Estadística individual de un jugador en un partido (amistoso o de torneo).</summary>
public class MatchPlayerStat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }

    public Guid EventId { get; set; }
    public virtual Event Event { get; set; } = null!;

    public Guid StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public bool IsStarter { get; set; }
    public int Minutes { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    /// <summary>Calificación del entrenador 1-10 (opcional).</summary>
    public int? Rating { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
