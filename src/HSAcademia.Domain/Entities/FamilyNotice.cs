namespace HSAcademia.Domain.Entities;

public enum FamilyNoticeType
{
    Arrival = 1
}

/// <summary>Aviso para la familia de un alumno, visible en su app (ej. "Tomás llegó").</summary>
public class FamilyNotice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public Guid StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    /// <summary>Sesión que originó el aviso.</summary>
    public Guid? EventId { get; set; }
    public FamilyNoticeType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
