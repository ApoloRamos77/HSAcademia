using System;

namespace HSAcademia.Domain.Entities;

public class Announcement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsPinned { get; set; } = false;
    
    public Guid AuthorId { get; set; }
    public virtual User Author { get; set; } = null!;
    
    public DateTime DatePosted { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}
