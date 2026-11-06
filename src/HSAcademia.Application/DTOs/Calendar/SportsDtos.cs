namespace HSAcademia.Application.DTOs.Calendar;

// ── Horarios recurrentes ─────────────────────────────────────────
public class CreateRecurringEventsDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? HeadquarterId { get; set; }
    public List<Guid> CategoryIds { get; set; } = new();
    public Guid? TeacherId { get; set; }
    /// <summary>Días de la semana: 0 = domingo … 6 = sábado.</summary>
    public List<int> DaysOfWeek { get; set; } = new();
    /// <summary>Hora local de inicio/fin "HH:mm".</summary>
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

public class RecurringEventsResultDto
{
    public Guid SeriesId { get; set; }
    public int Created { get; set; }
    public List<SkippedDateDto> Skipped { get; set; } = new();
}

public class SkippedDateDto
{
    public DateTime Date { get; set; }
    public string Reason { get; set; } = string.Empty;
}

// ── Resultado y estadísticas de partido ─────────────────────────
public class MatchResultDto
{
    public Guid EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Opponent { get; set; }
    public DateTime StartTime { get; set; }
    public int? GoalsFor { get; set; }
    public int? GoalsAgainst { get; set; }
    public string? MatchNotes { get; set; }
    public List<PlayerStatDto> Players { get; set; } = new();
}

public class PlayerStatDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    /// <summary>true si fue convocado (EventCall) o ya tiene estadística.</summary>
    public bool Called { get; set; }
    public bool Played { get; set; }
    public bool IsStarter { get; set; }
    public int Minutes { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public int? Rating { get; set; }
    public string? Notes { get; set; }
}

public class SaveMatchResultDto
{
    public int? GoalsFor { get; set; }
    public int? GoalsAgainst { get; set; }
    public string? MatchNotes { get; set; }
    /// <summary>Solo los jugadores que participaron.</summary>
    public List<SavePlayerStatDto> Players { get; set; } = new();
}

public class SavePlayerStatDto
{
    public Guid StudentId { get; set; }
    public bool IsStarter { get; set; }
    public int Minutes { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public int? Rating { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Totales del alumno en partidos (para su ficha y el portal de la familia).</summary>
public class StudentSportsSummaryDto
{
    public int Matches { get; set; }
    public int Starts { get; set; }
    public int Minutes { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public decimal? AverageRating { get; set; }
    public List<StudentMatchLineDto> RecentMatches { get; set; } = new();
}

public class StudentMatchLineDto
{
    public Guid EventId { get; set; }
    public DateTime Date { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Opponent { get; set; }
    public string? Score { get; set; }
    public int Minutes { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int? Rating { get; set; }
}
