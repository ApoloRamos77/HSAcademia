using System;

namespace HSAcademia.Application.DTOs.Calendar;

public class EventCallDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? StudentCategory { get; set; }
    public bool? IsConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateEventCallDto
{
    public bool? IsConfirmed { get; set; }
}
