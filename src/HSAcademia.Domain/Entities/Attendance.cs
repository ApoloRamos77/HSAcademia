using HSAcademia.Domain.Enums;

namespace HSAcademia.Domain.Entities;

public class Attendance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; } // Tenant ID
    public Guid StudentId { get; set; }
    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Optional: Links this attendance record to a specific calendar Event.
    /// When set, the 15-minute window rule is enforced before opening the roll call.
    /// </summary>
    public Guid? EventId { get; set; }

    // Quién marcó el estado actual, cuándo y cómo (null en registros anteriores a este control).
    public AttendanceSource? Source { get; set; }
    public Guid? MarkedByUserId { get; set; }
    public string? MarkedByName { get; set; }
    public DateTime? MarkedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public virtual Academy Academy { get; set; } = null!;
    public virtual Student Student { get; set; } = null!;
    public virtual Event? Event { get; set; }
}
