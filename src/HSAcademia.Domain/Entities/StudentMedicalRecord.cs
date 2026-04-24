using System;

namespace HSAcademia.Domain.Entities;

public class StudentMedicalRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    // Ficha Médica
    public string? Allergies { get; set; }
    public string? MedicalConditions { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    // Ficha Nutricional
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? BMI { get; set; } // IMC
    public string? NutritionPlan { get; set; }
}
