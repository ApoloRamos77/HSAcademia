namespace HSAcademia.Domain.Entities;

/// <summary>Formato de competencia del torneo.</summary>
public enum TournamentFormat
{
    Other = 0,
    /// <summary>Liga o campeonato continuo (todos contra todos, por fechas).</summary>
    League = 1,
    /// <summary>Copa con fase de grupos y/o eliminatorias.</summary>
    Cup = 2,
    /// <summary>Relámpago o festival de uno a pocos días.</summary>
    Festival = 3,
    /// <summary>Cuadrangular / triangular.</summary>
    Quadrangular = 4
}

/// <summary>Cómo se reparte el torneo en el calendario.</summary>
public enum TournamentScheduleMode
{
    /// <summary>Un solo día.</summary>
    SingleDay = 1,
    /// <summary>Rango de fechas con días de juego (ej. sábados y domingos).</summary>
    DateRange = 2,
    /// <summary>Continuo / temporada: fecha de fin opcional.</summary>
    Continuous = 3
}

public enum TournamentStatus
{
    Registration = 1,
    InProgress = 2,
    Finished = 3,
    Cancelled = 4
}

public class Tournament
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; } // Tenant
    public string Name { get; set; } = string.Empty;
    public string Organizer { get; set; } = string.Empty;
    public string MainLocation { get; set; } = string.Empty;

    public TournamentFormat Format { get; set; } = TournamentFormat.Other;
    public TournamentScheduleMode ScheduleMode { get; set; } = TournamentScheduleMode.DateRange;
    public TournamentStatus Status { get; set; } = TournamentStatus.Registration;
    /// <summary>Fecha de inicio (solo fecha). Null en torneos migrados sin partidos.</summary>
    public DateTime? StartDate { get; set; }
    /// <summary>Fecha de fin (solo fecha). Null = continuo sin fin conocido.</summary>
    public DateTime? EndDate { get; set; }
    /// <summary>Días de juego habituales (0 = domingo … 6 = sábado). Vacío = sin días fijos.</summary>
    public List<int> PlayDays { get; set; } = new();

    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    /// <summary>Reglamento, documentos exigidos y notas generales.</summary>
    public string? Rules { get; set; }

    /// <summary>Amarillas acumuladas que suspenden un partido (null = no se controla).</summary>
    public int? YellowCardsForSuspension { get; set; }
    /// <summary>Partidos de suspensión por tarjeta roja (null = no se controla).</summary>
    public int? RedCardSuspensionMatches { get; set; }
    /// <summary>Pide autorización del apoderado (viajes, salidas con menores).</summary>
    public bool RequiresAuthorization { get; set; }
    /// <summary>Texto que acepta el apoderado.</summary>
    public string? AuthorizationText { get; set; }
    /// <summary>Documentos que cada jugador debe entregar (ej. "DNI", "Certificado médico", "Carné de la liga").</summary>
    public List<string> RequiredDocuments { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public virtual Academy Academy { get; set; } = null!;
    public virtual ICollection<TournamentTeam> Teams { get; set; } = new List<TournamentTeam>();
}

/// <summary>
/// Participación de la academia en un torneo: una categoría (o varias) con su plantel y su entrenador responsable.
/// Una misma categoría puede inscribir varios equipos (Sub-12 A, Sub-12 B).
/// </summary>
public class TournamentTeam
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public Guid TournamentId { get; set; }

    public string Name { get; set; } = string.Empty;
    /// <summary>Categorías de las que sale el equipo (para convocar en bloque y para la agenda).</summary>
    public List<Guid> CategoryIds { get; set; } = new();
    /// <summary>Entrenador responsable en el torneo; puede no ser el de los entrenamientos.</summary>
    public Guid? HeadCoachId { get; set; }
    /// <summary>Asistente o delegado (opcional).</summary>
    public Guid? AssistantId { get; set; }

    /// <summary>Años de nacimiento permitidos por el reglamento (aviso, no bloqueo).</summary>
    public int? BirthYearFrom { get; set; }
    public int? BirthYearTo { get; set; }
    /// <summary>Tope de jugadores inscritos en la planilla del torneo.</summary>
    public int? MaxPlayers { get; set; }
    /// <summary>Honorario por partido del responsable (o su reemplazo). Con honorario, el partido no cuenta como sesión u hora.</summary>
    public decimal? MatchFee { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public virtual Tournament Tournament { get; set; } = null!;
    public virtual User? HeadCoach { get; set; }
    public virtual User? Assistant { get; set; }
    public virtual ICollection<TournamentPlayer> Players { get; set; } = new List<TournamentPlayer>();
}

/// <summary>Alumno inscrito en el plantel de un equipo del torneo. Cada partido del equipo lo convoca.</summary>
public class TournamentPlayer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public Guid TeamId { get; set; }
    public Guid StudentId { get; set; }

    public int? ShirtNumber { get; set; }
    public string? Position { get; set; }
    /// <summary>Documentos del torneo ya entregados por el jugador.</summary>
    public List<string> DeliveredDocuments { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual TournamentTeam Team { get; set; } = null!;
    public virtual Student Student { get; set; } = null!;
}

/// <summary>Autorización del apoderado para que el alumno participe en el torneo.</summary>
public class TournamentAuthorization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public Guid TournamentId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;
    public Guid? AcceptedByUserId { get; set; }
    public string? AcceptedByName { get; set; }
    /// <summary>"app" (la aceptó la familia) o "papel" (la registró la academia).</summary>
    public string Method { get; set; } = "app";
    public string? Note { get; set; }
    /// <summary>Texto aceptado, tal como estaba en ese momento.</summary>
    public string? TextSnapshot { get; set; }

    public virtual Tournament Tournament { get; set; } = null!;
    public virtual Student Student { get; set; } = null!;
}

/// <summary>Suspensión de un jugador para un partido por tarjetas (calculada; el administrador puede levantarla convocándolo).</summary>
public class TournamentSuspension
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public Guid TeamId { get; set; }
    public Guid StudentId { get; set; }
    /// <summary>Partido que no juega.</summary>
    public Guid EventId { get; set; }
    /// <summary>Partido donde recibió la tarjeta que la originó.</summary>
    public Guid SourceEventId { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>El administrador lo convocó igual: no se le vuelve a quitar.</summary>
    public bool Overridden { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual TournamentTeam Team { get; set; } = null!;
    public virtual Student Student { get; set; } = null!;
}
