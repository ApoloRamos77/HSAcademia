using System;
using System.Collections.Generic;

namespace HSAcademia.Domain.Entities;

public class PettyCash
{
    public Guid Id { get; set; }
    public Guid AcademyId { get; set; }
    public Guid? HeadquarterId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal AssignedAmount { get; set; }
    public decimal CurrentBalance { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Academy Academy { get; set; } = null!;
    public Headquarter? Headquarter { get; set; }
    
    public ICollection<PettyCashTransaction> Transactions { get; set; } = new List<PettyCashTransaction>();
}
