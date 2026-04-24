namespace HSAcademia.Domain.Enums;

public enum EventType
{
    Training = 1,
    FriendlyMatch = 2,
    TournamentMatch = 3,
    Birthday = 4 // Usualmente no se guarda en DB, se genera on-the-fly, pero se incluye por consistencia.
}
