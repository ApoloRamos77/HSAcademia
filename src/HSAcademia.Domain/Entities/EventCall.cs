using System;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Domain.Entities;

public class EventCall
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    
    public Guid EventId { get; set; }
    public Guid StudentId { get; set; }

    /// <summary>
    /// Null = Pending, True = Confirmed (Will attend), False = Rejected (Will not attend)
    /// </summary>
    public bool? IsConfirmed { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual Academy Academy { get; set; } = null!;
    public virtual Event Event { get; set; } = null!;
    public virtual Student Student { get; set; } = null!;
}
