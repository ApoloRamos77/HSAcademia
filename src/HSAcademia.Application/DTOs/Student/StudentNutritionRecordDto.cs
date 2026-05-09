using System;

namespace HSAcademia.Application.DTOs.Student;

public class StudentNutritionRecordDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? BMI { get; set; }
    public decimal? MuscleMassPercentage { get; set; }
    public decimal? FatPercentage { get; set; }
    public string? Notes { get; set; }
    public DateTime RecordDate { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public Guid? RegisteredById { get; set; }
    public string? RegisteredByName { get; set; }
}
