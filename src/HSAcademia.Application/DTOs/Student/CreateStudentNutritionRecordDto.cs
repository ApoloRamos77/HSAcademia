using System;

namespace HSAcademia.Application.DTOs.Student;

public class CreateStudentNutritionRecordDto
{
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? MuscleMassPercentage { get; set; }
    public decimal? FatPercentage { get; set; }
    public string? Notes { get; set; }
    public DateTime? RecordDate { get; set; }
}
