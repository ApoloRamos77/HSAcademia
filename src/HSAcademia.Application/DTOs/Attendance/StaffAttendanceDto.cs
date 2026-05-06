using HSAcademia.Domain.Enums;
using System;
using System.Collections.Generic;

namespace HSAcademia.Application.DTOs.Attendance;

public class StaffAttendanceDto
{
    public Guid StaffId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    
    public Guid? AttendanceId { get; set; }
    public AttendanceStatus? Status { get; set; }
    public string? Notes { get; set; }
}

public class MarkStaffAttendanceDto
{
    public DateTime Date { get; set; }
    public List<StaffAttendanceRecordDto> Records { get; set; } = new();
}

public class StaffAttendanceRecordDto
{
    public Guid StaffId { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Notes { get; set; }
}
