using System.ComponentModel.DataAnnotations;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Application.DTOs.Attendance;

/// <summary>Sesión del día con su entrenador y lo marcado por el coordinador o el administrador.</summary>
public class StaffSessionDto
{
    public Guid EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;       // YYYY-MM-DD
    public string StartTime { get; set; } = string.Empty;  // HH:mm
    public string CategoryName { get; set; } = string.Empty;
    public string HeadquarterName { get; set; } = string.Empty;

    /// <summary>Entrenador de la sesión; null si el calendario no tiene uno y aún no se indicó quién dictó.</summary>
    public Guid? StaffId { get; set; }
    public string? StaffName { get; set; }

    public AttendanceStatus? Status { get; set; }
    public Guid? SubstituteStaffId { get; set; }
    public string? SubstituteName { get; set; }
    public string? Notes { get; set; }
    public string? MarkedByName { get; set; }
    public DateTime? MarkedAt { get; set; }

    /// <summary>Por qué este usuario no puede marcarla; null si puede.</summary>
    public string? BlockedReason { get; set; }
}

public class StaffOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class SaveStaffSessionsDto
{
    [Required]
    public List<StaffSessionMarkDto> Records { get; set; } = new();
}

public class StaffSessionMarkDto
{
    [Required]
    public Guid EventId { get; set; }

    [Required]
    public AttendanceStatus Status { get; set; }

    /// <summary>Quién dictó, solo cuando la sesión no tiene entrenador en el calendario.</summary>
    public Guid? StaffId { get; set; }

    /// <summary>Quién reemplazó al entrenador ausente.</summary>
    public Guid? SubstituteStaffId { get; set; }
    public string? Notes { get; set; }

    /// <summary>Hora real de la marca en el celular, cuando se envía después por falta de señal.</summary>
    public DateTime? MarkedAt { get; set; }
}

/// <summary>Resumen del mes por entrenador. Solo cuenta sesiones ya iniciadas.</summary>
public class StaffMonthlyRowDto
{
    public Guid StaffId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Programmed { get; set; }
    /// <summary>Sesiones que realmente dictó, incluidos los reemplazos.</summary>
    public int Dictated { get; set; }
    public int Late { get; set; }
    public int Absent { get; set; }
    public int Excused { get; set; }
    public int Substitutions { get; set; }
    public int Unmarked { get; set; }
}
