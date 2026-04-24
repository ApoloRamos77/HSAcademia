using System.ComponentModel.DataAnnotations;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Application.DTOs.Attendance;

public class MarkAttendanceDto
{
    [Required]
    public DateTime Date { get; set; }

    [Required]
    public List<StudentAttendanceRecordDto> Records { get; set; } = new();
}

public class StudentAttendanceRecordDto
{
    [Required]
    public Guid StudentId { get; set; }

    [Required]
    public AttendanceStatus Status { get; set; }

    public string? Notes { get; set; }
}
