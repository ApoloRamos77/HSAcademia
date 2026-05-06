using System;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Domain.Entities;

public class StaffAttendance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public Guid StaffId { get; set; }
    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual Academy Academy { get; set; } = null!;
    public virtual User Staff { get; set; } = null!;
}
