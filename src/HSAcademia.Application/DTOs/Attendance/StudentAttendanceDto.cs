using HSAcademia.Domain.Enums;

namespace HSAcademia.Application.DTOs.Attendance;

public class StudentAttendanceDto
{
    public Guid StudentId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    
    // Si la asistencia ya fue tomada para la fecha, esto tendrá un valor
    public Guid? AttendanceId { get; set; }
    public AttendanceStatus? Status { get; set; }
    public string? Notes { get; set; }
}
