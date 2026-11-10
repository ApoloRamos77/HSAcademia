using System.ComponentModel.DataAnnotations;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Application.DTOs.Attendance;

public class MarkAttendanceDto
{
    [Required]
    public DateTime Date { get; set; }

    public Guid? EventId { get; set; }

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

    /// <summary>"QR" si se leyó el carné sin señal y se envía después; vacío = marcado en la lista.</summary>
    public string? Via { get; set; }

    /// <summary>Hora real de la marca en el celular, cuando se envía después por falta de señal.</summary>
    public DateTime? MarkedAt { get; set; }

    /// <summary>Solo aplica si el alumno sigue sin marcar (falta automática al cerrar): no pisa lo registrado por otro celular.</summary>
    public bool IfUnmarked { get; set; }
}
