using System;

namespace HSAcademia.Domain.Entities;

public class FinancialPeriod
{
    public Guid Id { get; set; }
    public Guid AcademyId { get; set; }
    public Academy Academy { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }
    
    public bool IsClosed { get; set; }
    
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public User? ClosedByUser { get; set; }
    
    public DateTime? ReopenedAt { get; set; }
    public Guid? ReopenedByUserId { get; set; }
    public User? ReopenedByUser { get; set; }
}
