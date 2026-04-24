using System.ComponentModel.DataAnnotations;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Application.DTOs.Attendance;

/// <summary>
/// DTO to open roll call for a specific calendar event.
/// The API validates that DateTime.UtcNow >= Event.StartTime - 15 minutes.
/// </summary>
public class MarkAttendanceByEventDto
{
    [Required]
    public Guid EventId { get; set; }

    [Required]
    public List<StudentAttendanceRecordDto> Records { get; set; } = new();
}
