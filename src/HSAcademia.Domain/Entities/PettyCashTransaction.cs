using System;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Domain.Entities;

public class PettyCashTransaction
{
    public Guid Id { get; set; }
    public Guid PettyCashId { get; set; }
    public PettyCashTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string Concept { get; set; } = null!;
    public DateTime Date { get; set; }
    public Guid? RegisteredById { get; set; }

    public PettyCash PettyCash { get; set; } = null!;
    public User? RegisteredBy { get; set; }
}
