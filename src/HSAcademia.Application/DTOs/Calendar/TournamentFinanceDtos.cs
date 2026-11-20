using System.ComponentModel.DataAnnotations;

namespace HSAcademia.Application.DTOs.Calendar;

public class SaveTournamentFeeDto
{
    [Required, MaxLength(80)] public string Name { get; set; } = string.Empty;
    /// <summary>Null = todos los equipos del torneo.</summary>
    public Guid? TeamId { get; set; }
    /// <summary>1 una sola vez, 2 por partido.</summary>
    [Range(1, 2)] public int Frequency { get; set; } = 2;
    /// <summary>1 monto por jugador, 2 costo total a repartir.</summary>
    [Range(1, 2)] public int AmountMode { get; set; } = 1;
    [Range(0.01, 100000)] public decimal Amount { get; set; }
    public DateTime? DueDate { get; set; }
    /// <summary>Por defecto se cobra a todos: la exoneración de becados se decide al cobrar.</summary>
    public bool ExemptScholarship { get; set; }
    public bool IsActive { get; set; } = true;
    [MaxLength(500)] public string? Notes { get; set; }
    /// <summary>Solo por partido: % de descuento desde el 2.º encuentro del mismo día (100 = exonerar). Null = conservar.</summary>
    [Range(0, 100)] public decimal? SameDayDiscountPercent { get; set; }
}

public class TournamentFeeDto
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public Guid? TeamId { get; set; }
    public string? TeamName { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public int AmountMode { get; set; }
    public decimal Amount { get; set; }
    public DateTime? DueDate { get; set; }
    public bool ExemptScholarship { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    /// <summary>Concepto automático de la ficha ("inscription" / "referee").</summary>
    public string? SystemKey { get; set; }
    public decimal? SameDayDiscountPercent { get; set; }

    public decimal Charged { get; set; }
    public decimal Collected { get; set; }
    public decimal Pending { get; set; }
    public int ChargedCount { get; set; }
    public int ReviewCount { get; set; }
    public int WaivedCount { get; set; }
    public int ExemptCount { get; set; }
}

public class TournamentChargeDto
{
    public Guid Id { get; set; }
    public Guid FeeId { get; set; }
    public string FeeName { get; set; } = string.Empty;
    public Guid? EventId { get; set; }
    public string? MatchLabel { get; set; }
    public DateTime? MatchDate { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int Status { get; set; }
    public decimal Amount { get; set; }
    public decimal AmountPaid { get; set; }
    public bool IsPaid { get; set; }
    public bool? Confirmed { get; set; }
    public string? DecidedByName { get; set; }
    public string? Note { get; set; }
    /// <summary>Exonerado a mano por el administrador aunque jugó.</summary>
    public bool ManualWaiver { get; set; }
}

public class DecideTournamentChargeDto
{
    /// <summary>true = cobrar, false = no cobrar.</summary>
    public bool Charge { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
}

public class TournamentExpenseDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Supplier { get; set; }
    public Guid? EventId { get; set; }
    public string? MatchLabel { get; set; }
}

public class SaveTournamentExpenseDto
{
    public DateTime Date { get; set; }
    [Range(0.01, 1000000)] public decimal Amount { get; set; }
    [Required, MaxLength(300)] public string Description { get; set; } = string.Empty;
    [MaxLength(200)] public string? Supplier { get; set; }
    public Guid? EventId { get; set; }
}

public class TournamentFinanceDto
{
    public decimal Charged { get; set; }
    public decimal Collected { get; set; }
    public decimal Pending { get; set; }
    public decimal Expenses { get; set; }
    /// <summary>Cobrado menos gastos.</summary>
    public decimal Result { get; set; }
    /// <summary>Si se cobra todo lo cargado.</summary>
    public decimal Projected { get; set; }
    public List<TournamentFeeDto> Fees { get; set; } = new();
    /// <summary>Confirmaron y no asistieron: esperan decisión.</summary>
    public List<TournamentChargeDto> Reviews { get; set; } = new();
    public List<TournamentExpenseDto> ExpenseItems { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    // Separado: lo del torneo (conceptos de una sola vez y gastos sin encuentro) y cada encuentro.
    public decimal OnceCharged { get; set; }
    public decimal OnceCollected { get; set; }
    public decimal OncePending { get; set; }
    public decimal GeneralExpenses { get; set; }
    public List<TournamentEncounterFinanceDto> Encounters { get; set; } = new();
}

/// <summary>Cobros y gastos de un encuentro (y su resultado).</summary>
public class TournamentEncounterFinanceDto
{
    public Guid EventId { get; set; }
    public DateTime StartTime { get; set; }
    public bool TimePending { get; set; }
    public string? Round { get; set; }
    public string? Opponent { get; set; }
    public string? Category { get; set; }
    public int MatchStatus { get; set; }
    /// <summary>Alumnos con cobro cargado.</summary>
    public int Players { get; set; }
    /// <summary>Convocados a los que no se cobra (faltó, exonerado…).</summary>
    public int NotCharged { get; set; }
    public decimal Charged { get; set; }
    public decimal Collected { get; set; }
    public decimal Pending { get; set; }
    public decimal Expenses { get; set; }
    /// <summary>Cobrado menos gastos del encuentro.</summary>
    public decimal Result { get; set; }
    public List<TournamentFeeLineDto> Lines { get; set; } = new();
    /// <summary>Solo en el detalle del encuentro.</summary>
    public List<TournamentChargeDto>? Charges { get; set; }
    public List<TournamentExpenseDto>? ExpenseItems { get; set; }
}

public class TournamentFeeLineDto
{
    public string FeeName { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Charged { get; set; }
    public decimal Collected { get; set; }
}

public class TournamentSyncResultDto
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Voided { get; set; }
    public int Review { get; set; }
    public List<string> Warnings { get; set; } = new();
}
