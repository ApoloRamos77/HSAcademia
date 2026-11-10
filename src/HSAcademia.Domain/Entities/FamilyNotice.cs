namespace HSAcademia.Domain.Entities;

public enum FamilyNoticeType
{
    Arrival = 1,
    PaymentProof = 2,
    EventCall = 3,
    PaymentReminder = 4
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
    /// <summary>Mensualidad que originó un recordatorio de pago.</summary>
    public Guid? PaymentRecordId { get; set; }
    /// <summary>Evita crear dos veces el mismo aviso programado (ej. "pay:{mensualidad}:-4").</summary>
    public string? DedupeKey { get; set; }
    public FamilyNoticeType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    /// <summary>Vacío mientras la notificación esté por enviar.</summary>
    public DateTime? PushedAt { get; set; }
}
