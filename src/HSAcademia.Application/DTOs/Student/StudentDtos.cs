using System;

namespace HSAcademia.Application.DTOs.Student;

public class StudentDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime DateOfBirth { get; set; }
    public int Age { get; set; }
    
    public Guid HeadquarterId { get; set; }
    public string HeadquarterName { get; set; } = string.Empty;
    
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    
    public Guid GuardianId { get; set; }
    public string GuardianName { get; set; } = string.Empty;
    public string GuardianPhone { get; set; } = string.Empty;
    public string GuardianEmail { get; set; } = string.Empty;
    
    public bool IsActive { get; set; }
    public DateTime EnrollmentDate { get; set; }
    public decimal? PreferentialFee { get; set; }
    public bool IsGuest { get; set; }
    public bool IsScholarship { get; set; }
    public decimal? ScholarshipPercentage { get; set; }
}

public class CreateStudentDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime DateOfBirth { get; set; }
    
    public Guid HeadquarterId { get; set; }
    public Guid CategoryId { get; set; }
    
    // Guardian details (if new) or ID (if existing)
    public Guid? GuardianId { get; set; }
    public string? GuardianFirstName { get; set; }
    public string? GuardianLastName { get; set; }
    public string? GuardianEmail { get; set; }
    public string? GuardianPhone { get; set; }

    // Enrollment and Financials
    public DateTime? EnrollmentDate { get; set; }
    public decimal? PreferentialFee { get; set; }
    public bool IsGuest { get; set; }
    public bool IsScholarship { get; set; }
    public decimal? ScholarshipPercentage { get; set; }

    // Medical Record
    public MedicalRecordDto MedicalRecord { get; set; } = new();
}

public class MedicalRecordDto
{
    public string? Allergies { get; set; }
    public string? MedicalConditions { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? BMI { get; set; }
    public string? NutritionPlan { get; set; }
}
