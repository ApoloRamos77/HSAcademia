namespace HSAcademia.Application.DTOs.Dashboard;

/// <summary>Panel principal de la academia. Los bloques sin permiso llegan en null.</summary>
public class AcademyDashboardDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public StudentsKpiDto Students { get; set; } = new();
    public FinanceKpiDto? Finance { get; set; }
    public AttendanceKpiDto Attendance { get; set; } = new();
    public List<DashboardEventDto> UpcomingEvents { get; set; } = new();
    public List<DashboardBirthdayDto> Birthdays { get; set; } = new();
    public List<DashboardAlertDto> Alerts { get; set; } = new();
}

public class StudentsKpiDto
{
    public int Active { get; set; }
    public int NewThisMonth { get; set; }
    public int WithdrawnThisMonth { get; set; }
    public int Scholarship { get; set; }
    public List<CategoryCountDto> ByCategory { get; set; } = new();
}

public class CategoryCountDto
{
    public Guid CategoryId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Headquarter { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class FinanceKpiDto
{
    /// <summary>Facturado del mes (mensualidades del periodo).</summary>
    public decimal Billed { get; set; }
    /// <summary>Cobrado en el mes (cualquier deuda, por fecha de pago).</summary>
    public decimal Collected { get; set; }
    /// <summary>Pendiente de las mensualidades del mes.</summary>
    public decimal Pending { get; set; }
    /// <summary>% del facturado del mes que ya está pagado.</summary>
    public decimal CollectionRate { get; set; }
    /// <summary>Deuda vencida acumulada (todos los periodos).</summary>
    public decimal OverdueAmount { get; set; }
    public int OverdueStudents { get; set; }
    public decimal StoreRevenue { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetBalance { get; set; }
    public List<FinanceTrendPointDto> Trend { get; set; } = new();
    public List<OverdueStudentDto> TopOverdue { get; set; } = new();
}

public class FinanceTrendPointDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Collected { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetBalance { get; set; }
}

public class OverdueStudentDto
{
    public Guid StudentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int MaxDaysOverdue { get; set; }
}

public class AttendanceKpiDto
{
    /// <summary>% de asistencia (presente + tarde) de los últimos 30 días; null si no hay registros.</summary>
    public decimal? RateLast30Days { get; set; }
    public int RecordsLast30Days { get; set; }
    public List<LowAttendanceStudentDto> LowAttendance { get; set; } = new();
    /// <summary>Alumnos activos con varias faltas seguidas: a quién llamar antes de que se retire.</summary>
    public List<AbsenceStreakDto> AbsenceStreaks { get; set; } = new();
}

public class AbsenceStreakDto
{
    public Guid StudentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Absences { get; set; }
    /// <summary>Fecha de la primera falta de la racha.</summary>
    public DateTime AbsentSince { get; set; }
    public string? ContactPhone { get; set; }
}

public class LowAttendanceStudentDto
{
    public Guid StudentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public int Records { get; set; }
}

public class DashboardEventDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Type { get; set; }
    public DateTime StartTime { get; set; }
    public string? Categories { get; set; }
    public string? Opponent { get; set; }
}

public class DashboardBirthdayDto
{
    public Guid StudentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int TurnsAge { get; set; }
    public bool IsToday { get; set; }
}

public class DashboardAlertDto
{
    /// <summary>"danger" | "warning" | "info"</summary>
    public string Level { get; set; } = "info";
    public string Message { get; set; } = string.Empty;
    /// <summary>Ruta del frontend donde se resuelve.</summary>
    public string? Link { get; set; }
}
