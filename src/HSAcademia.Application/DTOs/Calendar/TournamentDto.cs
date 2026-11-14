using System.ComponentModel.DataAnnotations;

namespace HSAcademia.Application.DTOs.Calendar;

/// <summary>Fila de la lista de torneos (también la usa el selector del Calendario).</summary>
public class TournamentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Organizer { get; set; } = string.Empty;
    public string MainLocation { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public int Format { get; set; }
    public int ScheduleMode { get; set; }
    public int Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<int> PlayDays { get; set; } = new();
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? Rules { get; set; }

    public int TeamsCount { get; set; }
    public int MatchesCount { get; set; }
    public int MatchesPlayed { get; set; }
    public DateTime? NextMatchAt { get; set; }
}

public class CreateTournamentDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Organizer { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string MainLocation { get; set; } = string.Empty;

    // Campos nuevos: opcionales para no romper clientes anteriores (null = conservar / valor por defecto).
    [Range(0, 4)] public int? Format { get; set; }
    [Range(1, 3)] public int? ScheduleMode { get; set; }
    [Range(1, 4)] public int? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<int>? PlayDays { get; set; }
    [MaxLength(150)] public string? ContactName { get; set; }
    [MaxLength(30)] public string? ContactPhone { get; set; }
    [MaxLength(4000)] public string? Rules { get; set; }
}

public class UpdateTournamentDto : CreateTournamentDto { }

// ─────────────────────────── Detalle ───────────────────────────

public class TournamentDetailDto : TournamentDto
{
    public List<TournamentTeamDto> Teams { get; set; } = new();
    /// <summary>Partidos del torneo (con y sin equipo asignado), ordenados por fecha.</summary>
    public List<TournamentMatchDto> Matches { get; set; } = new();
}

public class TournamentTeamDto
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Guid> CategoryIds { get; set; } = new();
    public List<string> CategoryNames { get; set; } = new();
    public Guid? HeadCoachId { get; set; }
    public string? HeadCoachName { get; set; }
    public Guid? AssistantId { get; set; }
    public string? AssistantName { get; set; }
    public int? BirthYearFrom { get; set; }
    public int? BirthYearTo { get; set; }
    public int? MaxPlayers { get; set; }
    public string? Notes { get; set; }
    public List<TournamentPlayerDto> Players { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class TournamentPlayerDto
{
    public Guid StudentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public DateTime? BirthDate { get; set; }
    public int? ShirtNumber { get; set; }
    public string? Position { get; set; }
    public bool IsActive { get; set; }
    /// <summary>Fuera de los años de nacimiento del reglamento (aviso, no bloqueo).</summary>
    public bool OutsideBirthYears { get; set; }
}

public class TournamentMatchDto
{
    public Guid Id { get; set; }
    public Guid? TeamId { get; set; }
    public string? TeamName { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime? CallTime { get; set; }
    public string? Round { get; set; }
    public string? OpponentTeam { get; set; }
    public string? Location { get; set; }
    public Guid? HeadquarterId { get; set; }
    public string? HeadquarterName { get; set; }
    public Guid? TeacherId { get; set; }
    public string? TeacherName { get; set; }
    /// <summary>El responsable del partido no es el del equipo (reemplazo puntual).</summary>
    public bool IsReplacement { get; set; }
    public int MatchStatus { get; set; }
    public int? GoalsFor { get; set; }
    public int? GoalsAgainst { get; set; }
    public int CalledCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int DeclinedCount { get; set; }
}

// ─────────────────────────── Equipos y plantel ───────────────────────────

public class SaveTournamentTeamDto
{
    [Required, MaxLength(120)] public string Name { get; set; } = string.Empty;
    public List<Guid> CategoryIds { get; set; } = new();
    public Guid? HeadCoachId { get; set; }
    public Guid? AssistantId { get; set; }
    [Range(1900, 2100)] public int? BirthYearFrom { get; set; }
    [Range(1900, 2100)] public int? BirthYearTo { get; set; }
    [Range(1, 200)] public int? MaxPlayers { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    /// <summary>Al cambiar de responsable, pasar también los partidos por jugar que tenía el anterior (por defecto sí).</summary>
    public bool ApplyCoachToUpcoming { get; set; } = true;
}

public class SaveTournamentPlayerDto
{
    public Guid StudentId { get; set; }
    [Range(0, 999)] public int? ShirtNumber { get; set; }
    [MaxLength(60)] public string? Position { get; set; }
}

public class SaveTournamentRosterDto
{
    public List<SaveTournamentPlayerDto> Players { get; set; } = new();
}

public class TournamentCandidateDto
{
    public Guid StudentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public DateTime? BirthDate { get; set; }
    public bool InTeamCategories { get; set; }
    public bool OutsideBirthYears { get; set; }
    /// <summary>Otros equipos de este torneo en los que ya figura.</summary>
    public List<string> OtherTeams { get; set; } = new();
}

public class RosterSaveResultDto
{
    public TournamentTeamDto Team { get; set; } = new();
    public int MatchesUpdated { get; set; }
    public int CallsAdded { get; set; }
    public int CallsRemoved { get; set; }
}

// ─────────────────────────── Partidos ───────────────────────────

public class SaveTournamentMatchDto
{
    /// <summary>Hora de pared de la academia.</summary>
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime? CallTime { get; set; }
    [MaxLength(80)] public string? Round { get; set; }
    [MaxLength(200)] public string? OpponentTeam { get; set; }
    [MaxLength(300)] public string? Location { get; set; }
    public Guid? HeadquarterId { get; set; }
    /// <summary>Responsable del partido; null = el del equipo.</summary>
    public Guid? TeacherId { get; set; }
    [Range(1, 6)] public int? MatchStatus { get; set; }
    [Range(0, 99)] public int? GoalsFor { get; set; }
    [Range(0, 99)] public int? GoalsAgainst { get; set; }
    [MaxLength(1000)] public string? Description { get; set; }
}

public class GenerateTournamentMatchesDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    /// <summary>0 = domingo … 6 = sábado.</summary>
    public List<int> DaysOfWeek { get; set; } = new();
    /// <summary>"HH:mm".</summary>
    [Required] public string StartTime { get; set; } = "09:00";
    [Required] public string EndTime { get; set; } = "10:30";
    /// <summary>Minutos de anticipación para la citación (0 = sin citación aparte).</summary>
    [Range(0, 600)] public int CallMinutesBefore { get; set; }
    [MaxLength(300)] public string? Location { get; set; }
    public Guid? HeadquarterId { get; set; }
    /// <summary>Prefijo de la fecha: "Fecha" → "Fecha 1", "Fecha 2"…</summary>
    [MaxLength(60)] public string? RoundPrefix { get; set; }
}

public class GenerateTournamentMatchesResultDto
{
    public int Created { get; set; }
    public List<GeneratedSkipDto> Skipped { get; set; } = new();
}

public class GeneratedSkipDto
{
    public DateTime Date { get; set; }
    public string Reason { get; set; } = string.Empty;
}
