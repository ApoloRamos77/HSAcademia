using HSAcademia.Domain.Enums;

namespace HSAcademia.Application.DTOs.Attendance;

public class AttendanceDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Notes { get; set; }
}
