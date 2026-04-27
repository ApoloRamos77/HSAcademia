using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Student;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

public class StudentService
{
    private readonly AppDbContext _context;

    public StudentService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<StudentDto>> GetStudentsAsync(Guid academyId)
    {
        var today = DateTime.UtcNow;
        var students = await _context.Students
            .Include(s => s.Headquarter)
            .Include(s => s.Category)
            .Include(s => s.Guardian)
            .Where(s => s.AcademyId == academyId)
            .ToListAsync();

        return students.Select(s => new StudentDto
        {
            Id = s.Id,
            FirstName = s.FirstName,
            LastName = s.LastName,
            DateOfBirth = s.DateOfBirth,
            Age = s.DateOfBirth > today.AddYears(- (today.Year - s.DateOfBirth.Year)) ? (today.Year - s.DateOfBirth.Year) - 1 : (today.Year - s.DateOfBirth.Year),
            HeadquarterId = s.HeadquarterId,
            HeadquarterName = s.Headquarter.Name,
            CategoryId = s.CategoryId,
            CategoryName = s.Category.Name,
            Email = s.Email,
            GuardianId = s.GuardianId,
            GuardianName = s.Guardian.FirstName + " " + s.Guardian.LastName,
            GuardianPhone = s.Guardian.Phone ?? "",
            GuardianEmail = s.Guardian.Email ?? "",
            IsActive = s.IsActive,
            EnrollmentDate = s.EnrollmentDate,
            PreferentialFee = s.PreferentialFee,
            IsGuest = s.IsGuest,
            IsScholarship = s.IsScholarship,
            ScholarshipPercentage = s.ScholarshipPercentage
        }).ToList();
    }

    public async Task<StudentDto> CreateStudentAsync(Guid academyId, CreateStudentDto dto)
    {
        Guid guardianId;

        // Either use existing or create new Guardian
        if (dto.GuardianId.HasValue && dto.GuardianId != Guid.Empty)
        {
            guardianId = dto.GuardianId.Value;
        }
        else
        {
            if (string.IsNullOrEmpty(dto.GuardianEmail) || string.IsNullOrEmpty(dto.GuardianFirstName))
                throw new Exception("Datos del apoderado incompletos.");

            // Check if user email exists
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.GuardianEmail);
            if (existingUser != null)
            {
                guardianId = existingUser.Id;
            }
            else
            {
                var newGuardian = new User
                {
                    AcademyId = academyId,
                    FirstName = dto.GuardianFirstName,
                    LastName = dto.GuardianLastName ?? "",
                    Email = dto.GuardianEmail,
                    Phone = dto.GuardianPhone,
                    Role = UserRole.Guardian,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"), // Default generic password
                    Status = UserStatus.Active
                };
                _context.Users.Add(newGuardian);
                await _context.SaveChangesAsync();
                guardianId = newGuardian.Id;
            }
        }

        var student = new Student
        {
            AcademyId = academyId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            DateOfBirth = dto.DateOfBirth.ToUniversalTime(),
            HeadquarterId = dto.HeadquarterId,
            CategoryId = dto.CategoryId,
            GuardianId = guardianId,
            IsActive = true,
            EnrollmentDate = dto.EnrollmentDate?.ToUniversalTime() ?? DateTime.UtcNow,
            PreferentialFee = dto.PreferentialFee,
            IsGuest = dto.IsGuest,
            IsScholarship = dto.IsScholarship,
            ScholarshipPercentage = dto.ScholarshipPercentage,
            Email = dto.Email
        };

        if (!string.IsNullOrEmpty(dto.Email))
        {
            var studentUser = new User
            {
                AcademyId = academyId,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Role = UserRole.Student,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                Status = UserStatus.Active
            };
            _context.Users.Add(studentUser);
            await _context.SaveChangesAsync();
            student.UserId = studentUser.Id;
        }

        if (dto.MedicalRecord != null)
        {
            student.MedicalRecord = new StudentMedicalRecord
            {
                Allergies = dto.MedicalRecord.Allergies,
                MedicalConditions = dto.MedicalRecord.MedicalConditions,
                EmergencyContactName = dto.MedicalRecord.EmergencyContactName,
                EmergencyContactPhone = dto.MedicalRecord.EmergencyContactPhone,
                WeightKg = dto.MedicalRecord.WeightKg,
                HeightCm = dto.MedicalRecord.HeightCm,
                BMI = dto.MedicalRecord.BMI,
                NutritionPlan = dto.MedicalRecord.NutritionPlan
            };
        }

        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        return new StudentDto
        {
            Id = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName
        };
    }

    public async Task<MedicalRecordDto> GetStudentMedicalRecordAsync(Guid academyId, Guid studentId)
    {
        var record = await _context.StudentMedicalRecords
            .Include(m => m.Student)
            .FirstOrDefaultAsync(m => m.StudentId == studentId && m.Student.AcademyId == academyId);

        if (record == null) return new MedicalRecordDto();

        return new MedicalRecordDto
        {
            Allergies = record.Allergies,
            MedicalConditions = record.MedicalConditions,
            EmergencyContactName = record.EmergencyContactName,
            EmergencyContactPhone = record.EmergencyContactPhone,
            WeightKg = record.WeightKg,
            HeightCm = record.HeightCm,
            BMI = record.BMI,
            NutritionPlan = record.NutritionPlan
        };
    }
    public async Task<StudentDto> UpdateStudentAsync(Guid academyId, Guid id, CreateStudentDto dto)
    {
        var student = await _context.Students
            .Include(s => s.MedicalRecord)
            .FirstOrDefaultAsync(s => s.Id == id && s.AcademyId == academyId);

        if (student == null) throw new Exception("Alumno no encontrado.");

        student.FirstName = dto.FirstName;
        student.LastName = dto.LastName;
        student.DateOfBirth = dto.DateOfBirth.ToUniversalTime();
        student.HeadquarterId = dto.HeadquarterId;
        student.CategoryId = dto.CategoryId;

        if (dto.EnrollmentDate.HasValue)
            student.EnrollmentDate = dto.EnrollmentDate.Value.ToUniversalTime();

        student.PreferentialFee = dto.PreferentialFee;
        student.IsGuest = dto.IsGuest;
        student.IsScholarship = dto.IsScholarship;
        student.ScholarshipPercentage = dto.ScholarshipPercentage;
        
        if (student.Email != dto.Email)
        {
            student.Email = dto.Email;
            if (student.UserId.HasValue)
            {
                var stUser = await _context.Users.FindAsync(student.UserId.Value);
                if (stUser != null) stUser.Email = dto.Email ?? "";
            }
            else if (!string.IsNullOrEmpty(dto.Email))
            {
                var newStUser = new User
                {
                    AcademyId = academyId,
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    Email = dto.Email,
                    Role = UserRole.Student,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                    Status = UserStatus.Active
                };
                _context.Users.Add(newStUser);
                await _context.SaveChangesAsync();
                student.UserId = newStUser.Id;
            }
        }

        // Update Guardian if exists
        var guardian = await _context.Users.FirstOrDefaultAsync(u => u.Id == student.GuardianId);
        if (guardian != null)
        {
            if (!string.IsNullOrEmpty(dto.GuardianFirstName)) guardian.FirstName = dto.GuardianFirstName;
            if (!string.IsNullOrEmpty(dto.GuardianLastName)) guardian.LastName = dto.GuardianLastName;
            if (!string.IsNullOrEmpty(dto.GuardianEmail)) guardian.Email = dto.GuardianEmail;
            if (!string.IsNullOrEmpty(dto.GuardianPhone)) guardian.Phone = dto.GuardianPhone;
        }

        if (dto.MedicalRecord != null)
        {
            if (student.MedicalRecord == null)
            {
                student.MedicalRecord = new StudentMedicalRecord { StudentId = student.Id };
            }
            student.MedicalRecord.Allergies = dto.MedicalRecord.Allergies;
            student.MedicalRecord.MedicalConditions = dto.MedicalRecord.MedicalConditions;
            student.MedicalRecord.EmergencyContactName = dto.MedicalRecord.EmergencyContactName;
            student.MedicalRecord.EmergencyContactPhone = dto.MedicalRecord.EmergencyContactPhone;
            student.MedicalRecord.WeightKg = dto.MedicalRecord.WeightKg;
            student.MedicalRecord.HeightCm = dto.MedicalRecord.HeightCm;
            student.MedicalRecord.BMI = dto.MedicalRecord.BMI;
            student.MedicalRecord.NutritionPlan = dto.MedicalRecord.NutritionPlan;
        }

        await _context.SaveChangesAsync();

        return new StudentDto
        {
            Id = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName
        };
    }
}
