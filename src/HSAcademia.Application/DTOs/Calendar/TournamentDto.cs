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
    public int Modality { get; set; }
    public int Level { get; set; }
    public decimal? InscriptionFee { get; set; }
    public decimal? RefereeFee { get; set; }
    public decimal? RefereeSameDayDiscountPercent { get; set; }
    public decimal? DefaultCoachFee { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<int> PlayDays { get; set; } = new();
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? Rules { get; set; }
    public int? YellowCardsForSuspension { get; set; }
    public int? RedCardSuspensionMatches { get; set; }
    public bool RequiresAuthorization { get; set; }
    public string? AuthorizationText { get; set; }
    public List<string> RequiredDocuments { get; set; } = new();

    public int TeamsCount { get; set; }
    public int MatchesCount { get; set; }
    public int MatchesPlayed { get; set; }
    public DateTime? NextMatchAt { get; set; }
    /// <summary>El próximo partido aún no tiene hora.</summary>
    public bool NextMatchTimePending { get; set; }
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
    [Range(1, 4)] public int? ScheduleMode { get; set; }
    /// <summary>0 otra, 5/7/9/11 = fútbol 5/7/9/11.</summary>
    [Range(0, 11)] public int? Modality { get; set; }
    [Range(0, 3)] public int? Level { get; set; }
    /// <summary>Por alumno; 0 = no se cobra; null = conservar.</summary>
    [Range(0, 100000)] public decimal? InscriptionFee { get; set; }
    [Range(0, 100000)] public decimal? RefereeFee { get; set; }
    /// <summary>% de descuento del arbitraje desde el 2.º encuentro del mismo día (100 = exonerar, 0 = sin descuento). Null = conservar.</summary>
    [Range(0, 100)] public decimal? RefereeSameDayDiscountPercent { get; set; }
    [Range(0, 100000)] public decimal? DefaultCoachFee { get; set; }
    [Range(1, 4)] public int? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<int>? PlayDays { get; set; }
    [MaxLength(150)] public string? ContactName { get; set; }
    [MaxLength(30)] public string? ContactPhone { get; set; }
    [MaxLength(4000)] public string? Rules { get; set; }

    // Fase C (opcionales: null = conservar)
    [Range(0, 20)] public int? YellowCardsForSuspension { get; set; }
    [Range(0, 20)] public int? RedCardSuspensionMatches { get; set; }
    public bool? RequiresAuthorization { get; set; }
    [MaxLength(2000)] public string? AuthorizationText { get; set; }
    public List<string>? RequiredDocuments { get; set; }
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
    public decimal? MatchFee { get; set; }
    public bool AutoRoster { get; set; }
    public int MatchesCount { get; set; }
    public TeamRecordDto Record { get; set; } = new();
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
    public List<string> DeliveredDocuments { get; set; } = new();
    public List<string> MissingDocuments { get; set; } = new();
    /// <summary>null = el torneo no pide autorización.</summary>
    public bool? Authorized { get; set; }
    public string? AuthorizationMethod { get; set; }
    /// <summary>Próximo partido que no juega por suspensión y motivo.</summary>
    public string? SuspendedFor { get; set; }
    /// <summary>Apto: documentos completos, autorizado (si se pide) y sin suspensión.</summary>
    public bool Fit { get; set; }
    public PlayerTournamentStatsDto Stats { get; set; } = new();
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
    public bool TimePending { get; set; }
    public decimal? CoachFee { get; set; }
    /// <summary>Falta la hora o el rival: no se puede tomar asistencia.</summary>
    public bool NeedsDetails { get; set; }
    public Guid? ReferenceCategoryId { get; set; }
    public string? ReferenceCategoryName { get; set; }
    /// <summary>Convocados de una categoría mayor que la de referencia.</summary>
    public int SuperiorCount { get; set; }
    public bool RefereeWaived { get; set; }
    public string? RefereeWaivedNote { get; set; }
}

/// <summary>Alumno que se puede convocar a un encuentro.</summary>
public class EncounterCandidateDto
{
    public Guid StudentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public Guid? HeadquarterId { get; set; }
    public DateTime? BirthDate { get; set; }
    /// <summary>Es de la categoría de referencia.</summary>
    public bool SameCategory { get; set; }
    /// <summary>Es mayor que la categoría de referencia (se resalta).</summary>
    public bool Superior { get; set; }
    /// <summary>Otros encuentros del mismo día en este torneo ("10:30 · Categoria 2012").</summary>
    public List<string> OtherMatches { get; set; } = new();
    public string? SuspendedFor { get; set; }
}

public class RefereeWaiverDto
{
    public bool Waived { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
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
    /// <summary>Honorario por partido del responsable (null o 0 = sin honorario).</summary>
    [Range(0, 100000)] public decimal? MatchFee { get; set; }
    /// <summary>Al cambiar de responsable, pasar también los partidos por jugar que tenía el anterior (por defecto sí).</summary>
    public bool ApplyCoachToUpcoming { get; set; } = true;
}

public class SaveTournamentPlayerDto
{
    public Guid StudentId { get; set; }
    [Range(0, 999)] public int? ShirtNumber { get; set; }
    [MaxLength(60)] public string? Position { get; set; }
    /// <summary>Documentos entregados (null = conservar).</summary>
    public List<string>? DeliveredDocuments { get; set; }
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
    /// <summary>Honorario del responsable por este partido (null = sin honorario propio).</summary>
    [Range(0, 100000)] public decimal? CoachFee { get; set; }
    /// <summary>Encuentro: categoría de referencia (null = conservar).</summary>
    public Guid? ReferenceCategoryId { get; set; }
    /// <summary>Encuentro: convocados exactos. Null = flujo anterior (convoca a todo el plantel al completar).</summary>
    public List<Guid>? StudentIds { get; set; }
}

/// <summary>Desde el calendario: categorías que juegan el torneo, con su responsable. Crea los días del torneo.</summary>
public class CreateParticipationDto
{
    public List<Guid> CategoryIds { get; set; } = new();
    public Guid? HeadCoachId { get; set; }
    /// <summary>Honorario por fecha (null = el sugerido del torneo).</summary>
    [Range(0, 100000)] public decimal? CoachFee { get; set; }
}

public class ParticipationResultDto
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int DaysCreated { get; set; }
    public int Players { get; set; }
    public List<string> Warnings { get; set; } = new();
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

// ─────────────────────────── Fase C ───────────────────────────

public class TournamentSuspensionDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public Guid EventId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool Overridden { get; set; }
}

public class TeamRecordDto
{
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int Points { get; set; }
}

public class MatchResultLineDto
{
    public DateTime Date { get; set; }
    public string? Round { get; set; }
    public string? Opponent { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
}

public class PlayerTournamentStatsDto
{
    public int Matches { get; set; }
    public int Minutes { get; set; }
    public int Goals { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
}

/// <summary>Torneo de un alumno para la app de la familia.</summary>
public class MyTournamentDto
{
    public Guid TournamentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Organizer { get; set; } = string.Empty;
    public int Status { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public int? ShirtNumber { get; set; }
    public bool RequiresAuthorization { get; set; }
    public string? AuthorizationText { get; set; }
    public bool Authorized { get; set; }
    public DateTime? AuthorizedAt { get; set; }
    public List<string> MissingDocuments { get; set; } = new();
    public string? SuspendedFor { get; set; }
    public TeamRecordDto Record { get; set; } = new();
    public string? NextMatch { get; set; }
    public List<MatchResultLineDto> LastResults { get; set; } = new();
    /// <summary>Encuentros a los que fue convocado el alumno (app 1.0.21).</summary>
    public List<MyEncounterDto> Encounters { get; set; } = new();
    /// <summary>Cobros del torneo que no son de un encuentro (inscripción, camiseta…). Servidor 1.0.28+.</summary>
    public List<string> TournamentChargeLines { get; set; } = new();
}

/// <summary>Encuentro del alumno: asistencia, resultado y lo que se le cobró.</summary>
public class MyEncounterDto
{
    public Guid EventId { get; set; }
    public DateTime StartTime { get; set; }
    public bool TimePending { get; set; }
    public string? Round { get; set; }
    public string? Opponent { get; set; }
    public string? Category { get; set; }
    public string? Location { get; set; }
    public int MatchStatus { get; set; }
    public int? GoalsFor { get; set; }
    public int? GoalsAgainst { get; set; }
    /// <summary>"present" | "late" | "absent" | "excused" | null (lista aún no tomada).</summary>
    public string? Attendance { get; set; }
    /// <summary>Cobrado en este encuentro (arbitraje y otros conceptos por partido).</summary>
    public decimal Charged { get; set; }
    public List<string> ChargeLines { get; set; } = new();
}

public class AuthorizeTournamentDto
{
    public Guid StudentId { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
}
