namespace HSAcademia.Domain.Enums;

public enum EventType
{
    Training = 1,
    FriendlyMatch = 2,
    TournamentMatch = 3,
    Birthday = 4 // Usualmente no se guarda en DB, se genera on-the-fly, pero se incluye por consistencia.
}

public enum MatchStatus
{
    Scheduled = 1,
    Played = 2,
    Postponed = 3,
    Suspended = 4,
    /// <summary>Ganado por walkover (el rival no se presentó).</summary>
    WalkoverWon = 5,
    /// <summary>Perdido por walkover (la academia no se presentó).</summary>
    WalkoverLost = 6
}
