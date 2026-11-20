namespace HSAcademia.Domain.Entities;

public enum StaffNoticeType
{
    /// <summary>Recordatorio al responsable de un encuentro (con hora o sin hora confirmada).</summary>
    MatchReminder = 1,
    /// <summary>Cumpleaños de alumnos del día (entrenadores de su categoría, coordinadores y administradores).</summary>
    Birthday = 2
}

/// <summary>
/// Aviso para un miembro del personal (push a sus celulares). Se guarda para no repetirlo (DedupeKey)
/// y el despachador de notificaciones lo envía aparte, igual que los avisos de las familias.
/// </summary>
public class StaffNotice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;
    public StaffNoticeType Type { get; set; }
    public Guid? EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? DedupeKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PushedAt { get; set; }
}
