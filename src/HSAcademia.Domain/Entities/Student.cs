using System;
using System.Collections.Generic;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Domain.Entities;

public class Student
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    
    // Guardian (Apoderado) is a User with Role = Guardian
    public Guid GuardianId { get; set; }
    public virtual User Guardian { get; set; } = null!;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    
    public Guid HeadquarterId { get; set; }
    public virtual Headquarter Headquarter { get; set; } = null!;
    
    public Guid CategoryId { get; set; }
    public virtual Category Category { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public DateTime EnrollmentDate { get; set; } = DateTime.UtcNow;

    // Financial Config
    public decimal? PreferentialFee { get; set; }
    public bool IsGuest { get; set; } = false;
    public bool IsScholarship { get; set; } = false;
    public decimal? ScholarshipPercentage { get; set; }
    public DateTime? PaymentStartDate { get; set; }

    // Navigation for One-to-One
    public virtual StudentMedicalRecord? MedicalRecord { get; set; }

    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}
