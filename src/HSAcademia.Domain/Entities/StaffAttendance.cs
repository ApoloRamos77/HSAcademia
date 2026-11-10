using System;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Domain.Entities;

/// <summary>Asistencia del entrenador a una sesión. La registra un coordinador o el administrador.</summary>
public class StaffAttendance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public Guid StaffId { get; set; }

    /// <summary>Sesión del calendario. Null solo en registros por día anteriores a este control.</summary>
    public Guid? EventId { get; set; }
    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Notes { get; set; }

    /// <summary>Quién dictó la sesión en lugar del entrenador ausente.</summary>
    public Guid? SubstituteStaffId { get; set; }

    public Guid? MarkedByUserId { get; set; }
    public string? MarkedByName { get; set; }
    public DateTime? MarkedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual Academy Academy { get; set; } = null!;
    public virtual User Staff { get; set; } = null!;
}
