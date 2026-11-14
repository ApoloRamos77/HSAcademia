using System;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Domain.Entities;

public class Expense
{
    public Guid Id { get; set; }
    public Guid AcademyId { get; set; }
    public ExpenseType Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = null!;
    public string? Supplier { get; set; }
    public string? VoucherUrl { get; set; }
    public Guid? RegisteredById { get; set; }
    /// <summary>Torneo al que corresponde el gasto (para su balance).</summary>
    public Guid? TournamentId { get; set; }
    /// <summary>Partido del torneo, si el gasto es de una fecha puntual.</summary>
    public Guid? TournamentEventId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Academy Academy { get; set; } = null!;
    public User? RegisteredBy { get; set; }
}
