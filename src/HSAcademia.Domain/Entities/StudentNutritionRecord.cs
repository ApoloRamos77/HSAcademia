using System;

namespace HSAcademia.Domain.Entities;

public class StudentNutritionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? BMI { get; set; }
    public decimal? MuscleMassPercentage { get; set; }
    public decimal? FatPercentage { get; set; }
    public string? Notes { get; set; }
    
    public DateTime RecordDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public Guid? RegisteredById { get; set; }
    public virtual User? RegisteredBy { get; set; }
}
