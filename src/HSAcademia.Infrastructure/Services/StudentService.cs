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

    public async Task<List<StudentDto>> GetStudentsAsync(Guid academyId, Guid? userId = null, string userRole = "")
    {
        var today = DateTime.UtcNow;
        var query = _context.Students
            .Include(s => s.Headquarter)
            .Include(s => s.Category)
            .Include(s => s.Guardian)
            .Where(s => s.AcademyId == academyId);

        if (userRole == "Staff" && userId.HasValue)
        {
            var userWithCategories = await _context.Users
                .Include(u => u.AssignedCategories)
                .FirstOrDefaultAsync(u => u.Id == userId.Value);

            if (userWithCategories != null && userWithCategories.AssignedCategories.Any())
            {
                var assignedCategoryIds = userWithCategories.AssignedCategories.Select(c => c.Id).ToList();
                query = query.Where(s => assignedCategoryIds.Contains(s.CategoryId));
            }
            else
            {
                // Si es staff pero no tiene categorias asignadas, no ve ningun alumno.
                return new List<StudentDto>();
            }
        }

        var students = await query.ToListAsync();

        return students.Select(s => new StudentDto
        {
            Id = s.Id,
            FirstName = s.FirstName,
            LastName = s.LastName,
            DocumentNumber = s.DocumentNumber,
            Phone = s.Phone,
            DateOfBirth = s.DateOfBirth,
            Age = s.DateOfBirth > today.AddYears(- (today.Year - s.DateOfBirth.Year)) ? (today.Year - s.DateOfBirth.Year) - 1 : (today.Year - s.DateOfBirth.Year),
            HeadquarterId = s.HeadquarterId,
            HeadquarterName = s.Headquarter.Name,
            CategoryId = s.CategoryId,
            CategoryName = s.Category.Name,
            Email = s.Email,
            GuardianId = s.GuardianId,
            GuardianName = s.Guardian != null ? s.Guardian.FirstName + " " + s.Guardian.LastName : "",
            GuardianPhone = s.Guardian?.Phone ?? "",
            GuardianEmail = s.Guardian?.Email ?? "",
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
        if (!string.IsNullOrEmpty(dto.DocumentNumber))
        {
            var existingDoc = await _context.Students.FirstOrDefaultAsync(s => s.AcademyId == academyId && s.DocumentNumber == dto.DocumentNumber);
            if (existingDoc != null)
                throw new Exception("Ya existe un alumno con ese número de documento en la academia.");
        }

        Guid? guardianId = null;

        if (dto.GuardianId.HasValue && dto.GuardianId != Guid.Empty)
        {
            guardianId = dto.GuardianId!.Value;
        }
        else if (!string.IsNullOrEmpty(dto.GuardianEmail) && !string.IsNullOrEmpty(dto.GuardianFirstName) && !string.IsNullOrEmpty(dto.GuardianPhone))
        {
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
                    FirstName = dto.GuardianFirstName!,
                    LastName = dto.GuardianLastName ?? "",
                    Email = dto.GuardianEmail!,
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
            DocumentNumber = dto.DocumentNumber,
            Phone = dto.Phone,
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

        if (!string.IsNullOrEmpty(dto.Email) && !string.IsNullOrEmpty(dto.Phone))
        {
            var existingStudentUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (existingStudentUser != null)
            {
                var linkedStudent = await _context.Students.FirstOrDefaultAsync(s => s.AcademyId == academyId && s.UserId == existingStudentUser.Id);
                if (linkedStudent != null)
                    throw new Exception("El correo electrónico ya pertenece a otro alumno registrado en esta sede.");

                student.UserId = existingStudentUser.Id;
            }
            else
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
                NutritionPlan = dto.MedicalRecord.NutritionPlan,
                NextNutritionConsultation = dto.MedicalRecord.NextNutritionConsultation?.ToUniversalTime()
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
            NutritionPlan = record.NutritionPlan,
            NextNutritionConsultation = record.NextNutritionConsultation
        };
    }

    /// <summary>Returns the mobile profile for the logged-in student or guardian's student.</summary>
    public async Task<MobileStudentProfileDto?> GetMyProfileAsync(Guid academyId, Guid userId)
    {
        var student = await _context.Students
            .Include(s => s.Headquarter)
            .Include(s => s.Category)
            .Include(s => s.Guardian)
            .Include(s => s.MedicalRecord)
            .FirstOrDefaultAsync(s =>
                s.AcademyId == academyId &&
                !s.IsDeleted &&
                (s.UserId == userId || s.GuardianId == userId));

        if (student == null) return null;

        var today = DateTime.UtcNow;
        int age = today.Year - student.DateOfBirth.Year;
        if (student.DateOfBirth.Date > today.AddYears(-age)) age--;

        return new MobileStudentProfileDto
        {
            Id              = student.Id,
            FullName        = $"{student.FirstName} {student.LastName}",
            FirstName       = student.FirstName,
            LastName        = student.LastName,
            DocumentNumber  = student.DocumentNumber,
            Email           = student.Email,
            DateOfBirth     = student.DateOfBirth,
            Age             = age,
            EnrollmentDate  = student.EnrollmentDate,
            CategoryName    = student.Category?.Name ?? "-",
            HeadquarterName = student.Headquarter?.Name ?? "-",
            GuardianName    = $"{student.Guardian?.FirstName} {student.Guardian?.LastName}".Trim(),
            GuardianPhone   = student.Guardian?.Phone ?? "-",
            // Nutrition
            WeightKg                  = student.MedicalRecord?.WeightKg,
            HeightCm                  = student.MedicalRecord?.HeightCm,
            BMI                       = student.MedicalRecord?.BMI,
            NutritionPlan             = student.MedicalRecord?.NutritionPlan,
            NextNutritionConsultation = student.MedicalRecord?.NextNutritionConsultation,
            // Medical
            Allergies              = student.MedicalRecord?.Allergies,
            MedicalConditions      = student.MedicalRecord?.MedicalConditions,
            EmergencyContactName   = student.MedicalRecord?.EmergencyContactName,
            EmergencyContactPhone  = student.MedicalRecord?.EmergencyContactPhone,
        };
    }
    public async Task<StudentDto> UpdateStudentAsync(Guid academyId, Guid id, CreateStudentDto dto)
    {
        var student = await _context.Students
            .Include(s => s.MedicalRecord)
            .FirstOrDefaultAsync(s => s.Id == id && s.AcademyId == academyId);

        if (student == null) throw new Exception("Alumno no encontrado.");

        if (!string.IsNullOrEmpty(dto.DocumentNumber) && student.DocumentNumber != dto.DocumentNumber)
        {
            var existingDoc = await _context.Students.FirstOrDefaultAsync(s => s.AcademyId == academyId && s.DocumentNumber == dto.DocumentNumber);
            if (existingDoc != null)
                throw new Exception("Ya existe un alumno con ese número de documento en la academia.");
        }

        student.FirstName = dto.FirstName;
        student.LastName = dto.LastName;
        student.DocumentNumber = dto.DocumentNumber;
        student.DateOfBirth = dto.DateOfBirth.ToUniversalTime();
        student.HeadquarterId = dto.HeadquarterId;
        student.CategoryId = dto.CategoryId;
        student.IsActive = dto.IsActive;

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
        }
        
        student.Phone = dto.Phone;
        
        // Try creating User if it doesn't exist but now we have Email & Phone
        if (!student.UserId.HasValue && !string.IsNullOrEmpty(dto.Email) && !string.IsNullOrEmpty(dto.Phone))
        {
            var existingStudentUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (existingStudentUser != null)
            {
                var linkedStudent = await _context.Students.FirstOrDefaultAsync(s => s.AcademyId == academyId && s.UserId == existingStudentUser.Id);
                if (linkedStudent != null && linkedStudent.Id != student.Id)
                    throw new Exception("El correo electrónico ya pertenece a otro alumno registrado en esta sede.");

                student.UserId = existingStudentUser.Id;
            }
            else
            {
                var newStUser = new User
                {
                    AcademyId = academyId,
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    Email = dto.Email,
                    Phone = dto.Phone,
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
            student.MedicalRecord.NextNutritionConsultation = dto.MedicalRecord.NextNutritionConsultation?.ToUniversalTime();
        }

        await _context.SaveChangesAsync();

        return new StudentDto
        {
            Id = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName
        };
    }

    /// <summary>Returns all students assigned to the Guardian user.</summary>
    public async Task<List<StudentDto>> GetMyStudentsAsync(Guid academyId, Guid guardianUserId)
    {
        var today = DateTime.UtcNow;
        var students = await _context.Students
            .Include(s => s.Headquarter)
            .Include(s => s.Category)
            .Include(s => s.Guardian)
            .Where(s => s.AcademyId == academyId && !s.IsDeleted &&
                       (s.GuardianId == guardianUserId || s.UserId == guardianUserId))
            .ToListAsync();

        return students.Select(s => new StudentDto
        {
            Id = s.Id,
            FirstName = s.FirstName,
            LastName = s.LastName,
            DocumentNumber = s.DocumentNumber,
            Phone = s.Phone,
            DateOfBirth = s.DateOfBirth,
            Age = today.Year - s.DateOfBirth.Year - (s.DateOfBirth.Date > today.AddYears(-(today.Year - s.DateOfBirth.Year)) ? 1 : 0),
            HeadquarterId = s.HeadquarterId,
            HeadquarterName = s.Headquarter?.Name ?? "",
            CategoryId = s.CategoryId,
            CategoryName = s.Category?.Name ?? "",
            Email = s.Email,
            GuardianId = s.GuardianId,
            GuardianName = s.Guardian != null ? s.Guardian.FirstName + " " + s.Guardian.LastName : "",
            GuardianPhone = s.Guardian?.Phone ?? "",
            GuardianEmail = s.Guardian?.Email ?? "",
            IsActive = s.IsActive,
            EnrollmentDate = s.EnrollmentDate,
            PreferentialFee = s.PreferentialFee,
            IsGuest = s.IsGuest,
            IsScholarship = s.IsScholarship,
            ScholarshipPercentage = s.ScholarshipPercentage
        }).ToList();
    }

    public async Task<List<StudentNutritionRecordDto>> GetStudentNutritionRecordsAsync(Guid academyId, Guid studentId)
    {
        var records = await _context.StudentNutritionRecords
            .Include(r => r.Student)
            .Include(r => r.RegisteredBy)
            .Where(r => r.StudentId == studentId && r.Student.AcademyId == academyId)
            .OrderByDescending(r => r.RecordDate)
            .ToListAsync();

        return records.Select(r => new StudentNutritionRecordDto
        {
            Id = r.Id,
            StudentId = r.StudentId,
            WeightKg = r.WeightKg,
            HeightCm = r.HeightCm,
            BMI = r.BMI,
            MuscleMassPercentage = r.MuscleMassPercentage,
            FatPercentage = r.FatPercentage,
            Notes = r.Notes,
            RecordDate = r.RecordDate,
            CreatedAt = r.CreatedAt,
            RegisteredById = r.RegisteredById,
            RegisteredByName = r.RegisteredBy != null ? $"{r.RegisteredBy.FirstName} {r.RegisteredBy.LastName}" : null
        }).ToList();
    }

    public async Task<StudentNutritionRecordDto> AddStudentNutritionRecordAsync(Guid academyId, Guid studentId, CreateStudentNutritionRecordDto dto, Guid registeredById)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == studentId && s.AcademyId == academyId);
        if (student == null) throw new Exception("Alumno no encontrado.");

        decimal? calculatedBmi = null;
        if (dto.WeightKg.HasValue && dto.HeightCm.HasValue && dto.HeightCm.Value > 0)
        {
            var heightMeters = dto.HeightCm.Value / 100m;
            calculatedBmi = dto.WeightKg.Value / (heightMeters * heightMeters);
        }

        var record = new StudentNutritionRecord
        {
            StudentId = studentId,
            WeightKg = dto.WeightKg,
            HeightCm = dto.HeightCm,
            BMI = calculatedBmi,
            MuscleMassPercentage = dto.MuscleMassPercentage,
            FatPercentage = dto.FatPercentage,
            Notes = dto.Notes,
            RecordDate = dto.RecordDate?.ToUniversalTime() ?? DateTime.UtcNow,
            RegisteredById = registeredById
        };

        _context.StudentNutritionRecords.Add(record);
        
        // Optionally update the latest in MedicalRecord as well for quick view
        var medicalRecord = await _context.StudentMedicalRecords.FirstOrDefaultAsync(m => m.StudentId == studentId);
        if (medicalRecord != null)
        {
            medicalRecord.WeightKg = record.WeightKg;
            medicalRecord.HeightCm = record.HeightCm;
            medicalRecord.BMI = record.BMI;
        }

        await _context.SaveChangesAsync();

        var registeredBy = await _context.Users.FindAsync(registeredById);

        return new StudentNutritionRecordDto
        {
            Id = record.Id,
            StudentId = record.StudentId,
            WeightKg = record.WeightKg,
            HeightCm = record.HeightCm,
            BMI = record.BMI,
            MuscleMassPercentage = record.MuscleMassPercentage,
            FatPercentage = record.FatPercentage,
            Notes = record.Notes,
            RecordDate = record.RecordDate,
            CreatedAt = record.CreatedAt,
            RegisteredById = record.RegisteredById,
            RegisteredByName = registeredBy != null ? $"{registeredBy.FirstName} {registeredBy.LastName}" : null
        };
    }
}
