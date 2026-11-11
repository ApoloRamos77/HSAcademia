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
    private readonly AuditService? _audit;
    private readonly AccessScopeService _scope;

    public StudentService(AppDbContext context, AccessScopeService scope, AuditService? audit = null)
    {
        _scope = scope;
        _context = context;
        _audit = audit;
    }

    public async Task<List<StudentDto>> GetStudentsAsync(Guid academyId, Guid? userId = null, string userRole = "")
    {
        var today = DateTime.UtcNow;
        var query = _context.Students
            .Include(s => s.Headquarter)
            .Include(s => s.Category)
            .Include(s => s.Guardian)
            .Where(s => s.AcademyId == academyId);

        // Solo el administrador ve a los retirados; el resto del personal trabaja con los activos.
        if (userRole != "AcademyAdmin" && userRole != "SuperAdmin")
            query = query.Where(s => s.IsActive);
        if (userRole == "Staff" && userId.HasValue)
            query = AccessScopeService.ApplyStudents(query, await _scope.ForUserAsync(userId.Value));

        var students = await query.ToListAsync();

        return students.Select(s => new StudentDto
        {
            Id = s.Id,
            FirstName = s.FirstName,
            LastName = s.LastName,
            DocumentNumber = s.DocumentNumber,
            Phone = s.Phone,
            DateOfBirth = s.DateOfBirth,
            Age = s.DateOfBirth.HasValue 
                ? (s.DateOfBirth.Value > today.AddYears(- (today.Year - s.DateOfBirth.Value.Year)) ? (today.Year - s.DateOfBirth.Value.Year) - 1 : (today.Year - s.DateOfBirth.Value.Year))
                : 0,
            HeadquarterId = s.HeadquarterId,
            HeadquarterName = s.Headquarter.Name,
            CategoryId = s.CategoryId,
            CategoryName = s.Category.Name,
            Email = s.Email,
            GuardianId = s.GuardianId,
            GuardianName = s.Guardian != null ? s.Guardian.FirstName + " " + s.Guardian.LastName : "", GuardianFirstName = s.Guardian != null ? s.Guardian.FirstName : "", GuardianLastName = s.Guardian != null ? s.Guardian.LastName : "",
            GuardianPhone = s.Guardian?.Phone ?? "",
            GuardianEmail = IsPlaceholderEmail(s.Guardian?.Email) ? "" : s.Guardian?.Email ?? "",
            IsActive = s.IsActive,
            EnrollmentDate = s.EnrollmentDate,
            PaymentStartDate = s.PaymentStartDate,
            WithdrawalDate = s.WithdrawalDate,
            PreferentialFee = s.PreferentialFee,
            BillingPlan = s.BillingPlan.ToString(),
            PreferentialSessionFee = s.PreferentialSessionFee,
            IsGuest = s.IsGuest,
            IsScholarship = s.IsScholarship,
            ScholarshipPercentage = s.ScholarshipPercentage
        }).ToList();
    }

    /// <param name="canEditFinancials">
    /// false para personal sin permiso de finanzas (p. ej. coordinadores de campo): no puede asignar
    /// becas, tarifas preferenciales ni la condición de invitado.
    /// </param>
    public async Task<StudentDto> CreateStudentAsync(Guid academyId, CreateStudentDto dto, bool canEditFinancials = true)
    {
        if (!canEditFinancials)
        {
            dto.PreferentialFee = null;
            dto.BillingPlan = null;
            dto.PreferentialSessionFee = null;
            dto.IsGuest = false;
            dto.IsScholarship = false;
            dto.ScholarshipPercentage = null;
            dto.PaymentStartDate = null;
        }
        ValidateStudentInput(dto, isCreate: true);
        await EnsureNotDuplicateAsync(academyId, dto, null);

        if (!string.IsNullOrEmpty(dto.DocumentNumber))
        {
            var existingDoc = await _context.Students.FirstOrDefaultAsync(s => s.AcademyId == academyId && s.DocumentNumber == dto.DocumentNumber);
            if (existingDoc != null)
                throw new Exception("Ya existe un alumno con ese número de documento en la academia.");
        }

        await ValidateHeadquarterAndCategoryAsync(academyId, dto.HeadquarterId, dto.CategoryId);

        var credentials = new List<GeneratedCredentialDto>();
        var guardianId = await ResolveGuardianAsync(academyId, dto, credentials);

        var student = new Student
        {
            AcademyId = academyId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            DateOfBirth = dto.DateOfBirth?.ToUniversalTime(),
            DocumentNumber = dto.DocumentNumber,
            Phone = dto.Phone,
            HeadquarterId = dto.HeadquarterId,
            CategoryId = dto.CategoryId,
            GuardianId = guardianId,
            IsActive = true,
            EnrollmentDate = dto.EnrollmentDate?.ToUniversalTime() ?? DateTime.UtcNow,
            PaymentStartDate = dto.PaymentStartDate?.ToUniversalTime(),
            WithdrawalDate = dto.WithdrawalDate?.ToUniversalTime(),
            PreferentialFee = dto.PreferentialFee,
            BillingPlan = ParsePlan(dto.BillingPlan) ?? StudentBillingPlan.Monthly,
            PreferentialSessionFee = dto.BillingPlan != null ? dto.PreferentialSessionFee : null,
            IsGuest = dto.IsGuest,
            IsScholarship = dto.IsScholarship,
            ScholarshipPercentage = dto.ScholarshipPercentage,
            Email = dto.Email
        };

        if (!string.IsNullOrWhiteSpace(dto.Email) && !string.IsNullOrWhiteSpace(dto.Phone))
        {
            student.Email = NormalizeEmail(dto.Email);
            var credential = await LinkOrCreateStudentUserAsync(academyId, student, dto.Phone);
            if (credential != null) credentials.Add(credential);
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

        await EnsurePlanIsConfiguredAsync(student);
        _context.Students.Add(student);
        _audit?.Add(academyId, "students.create", nameof(Student), student.Id, new { dto.FirstName, dto.LastName, dto.CategoryId, outsideCategoryAge = await OutsideCategoryAgeAsync(dto.CategoryId, dto.DateOfBirth) });
        // Un único SaveChanges: si algo falla no quedan usuarios huérfanos creados.
        await _context.SaveChangesAsync();

        return new StudentDto
        {
            Id = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName,
            BillingPlan = student.BillingPlan.ToString(),
            PreferentialSessionFee = student.PreferentialSessionFee,
            GeneratedCredentials = credentials.Count > 0 ? credentials : null
        };
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers de cuentas de usuario (alumno / apoderado)
    private const string NoEmailDomain = "@sin-correo.clubio";

    /// <summary>Apoderados registrados sin correo usan un correo interno (ingresan con su celular).</summary>
    public static bool IsPlaceholderEmail(string? email) =>
        email != null && email.EndsWith(NoEmailDomain, StringComparison.OrdinalIgnoreCase);

    private static string Digits(string? s) => new string((s ?? string.Empty).Where(char.IsDigit).ToArray());

    private static readonly System.Text.RegularExpressions.Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Reglas de negocio del alumno (también aplican a la web).</summary>
    private static void ValidateStudentInput(CreateStudentDto dto, bool isCreate)
    {
        if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
            throw new Exception("Los nombres y apellidos del alumno son obligatorios.");
        if (dto.FirstName.Trim().Length > 100 || dto.LastName.Trim().Length > 100)
            throw new Exception("Los nombres y apellidos no pueden superar 100 caracteres.");

        if (dto.DateOfBirth.HasValue)
        {
            var dob = dto.DateOfBirth.Value.Date;
            if (dob > AcademyClock.Today) throw new Exception("La fecha de nacimiento no puede ser futura.");
            if (dob.Year < 1940) throw new Exception("Revisa la fecha de nacimiento.");
        }

        void Phone(string? value, string label)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var d = Digits(value);
            if (d.Length < 7 || d.Length > 15) throw new Exception($"El {label} no es válido.");
        }
        Phone(dto.Phone, "celular del alumno");
        Phone(dto.GuardianPhone, "celular del apoderado");
        Phone(dto.MedicalRecord?.EmergencyContactPhone, "teléfono de emergencia");

        if (!string.IsNullOrWhiteSpace(dto.Email) && !EmailRegex.IsMatch(dto.Email.Trim()))
            throw new Exception("El correo del alumno no es válido.");
        if (!string.IsNullOrWhiteSpace(dto.GuardianEmail) && !EmailRegex.IsMatch(dto.GuardianEmail.Trim()))
            throw new Exception("El correo del apoderado no es válido.");

        if (dto.ScholarshipPercentage is < 0 or > 100) throw new Exception("El porcentaje de beca debe estar entre 0 y 100.");
        if (dto.PreferentialFee is < 0) throw new Exception("La tarifa preferencial no puede ser negativa.");
        if (dto.PreferentialSessionFee is < 0) throw new Exception("El precio por sesión no puede ser negativo.");
        if (dto.BillingPlan != null && ParsePlan(dto.BillingPlan) == null) throw new Exception("Plan de cobro no válido.");
        if (dto.EnrollmentDate.HasValue && dto.WithdrawalDate.HasValue && dto.WithdrawalDate.Value.Date < dto.EnrollmentDate.Value.Date)
            throw new Exception("La fecha de retiro no puede ser anterior a la de inscripción.");

        // Al inscribir, debe existir al menos un medio de contacto (seguridad del menor y cobranza).
        if (isCreate && string.IsNullOrWhiteSpace(dto.Phone) && string.IsNullOrWhiteSpace(dto.GuardianPhone)
            && !(dto.GuardianId.HasValue && dto.GuardianId != Guid.Empty))
            throw new Exception("Registra el celular del apoderado o del alumno para poder contactarlo.");
    }

    /// <summary>Evita inscribir dos veces al mismo alumno (mismo nombre y fecha de nacimiento).</summary>
    private async Task EnsureNotDuplicateAsync(Guid academyId, CreateStudentDto dto, Guid? excludeId)
    {
        if (!dto.DateOfBirth.HasValue) return;
        var fn = dto.FirstName.Trim().ToLower();
        var ln = dto.LastName.Trim().ToLower();
        var dob = dto.DateOfBirth.Value.Date;
        var candidates = await _context.Students
            .Where(s => s.AcademyId == academyId && (excludeId == null || s.Id != excludeId) && s.DateOfBirth != null &&
                        s.FirstName.ToLower() == fn && s.LastName.ToLower() == ln)
            .Select(s => new { s.DateOfBirth, s.IsActive })
            .ToListAsync();
        var dup = candidates.FirstOrDefault(c => c.DateOfBirth!.Value.Date == dob);
        if (dup != null)
            throw new Exception(dup.IsActive
                ? "Ya existe un alumno con el mismo nombre y fecha de nacimiento."
                : "Este alumno ya está registrado como retirado: búscalo y reactívalo en lugar de inscribirlo de nuevo.");
    }

    /// <summary>
    /// Obtiene el apoderado del alumno: uno existente (GuardianId), uno de la academia con el mismo
    /// correo o celular (hermanos), o crea uno nuevo con contraseña temporal. El correo es opcional:
    /// sin correo, el apoderado ingresa a la app con su celular.
    /// </summary>
    private async Task<Guid?> ResolveGuardianAsync(Guid academyId, CreateStudentDto dto, List<GeneratedCredentialDto> credentials)
    {
        if (dto.GuardianId.HasValue && dto.GuardianId != Guid.Empty)
        {
            var ok = await _context.Users.AnyAsync(u => u.Id == dto.GuardianId.Value && u.AcademyId == academyId && u.Role == UserRole.Guardian);
            if (!ok) throw new Exception("El apoderado seleccionado no es válido.");
            return dto.GuardianId.Value;
        }

        var hasName = !string.IsNullOrWhiteSpace(dto.GuardianFirstName);
        var hasPhone = !string.IsNullOrWhiteSpace(dto.GuardianPhone);
        var hasEmail = !string.IsNullOrWhiteSpace(dto.GuardianEmail);
        if (!hasName && !hasPhone && !hasEmail) return null;
        if (!hasName || !hasPhone)
            throw new Exception("Para registrar al apoderado indica al menos su nombre y su celular.");

        var phone = Digits(dto.GuardianPhone);

        if (hasEmail)
        {
            var guardianEmail = NormalizeEmail(dto.GuardianEmail!);
            var existingUser = await FindUserByEmailAsync(guardianEmail);
            if (existingUser != null)
            {
                if (existingUser.IsDeleted || existingUser.AcademyId != academyId || existingUser.Role != UserRole.Guardian)
                    throw new Exception($"El correo {guardianEmail} ya está registrado en otra cuenta. Usa un correo distinto para el apoderado.");
                return existingUser.Id;
            }
        }
        else
        {
            // Sin correo: si la academia ya tiene un apoderado con ese celular (hermanos), se reutiliza.
            var sameAcademyGuardians = await _context.Users
                .Where(u => u.AcademyId == academyId && u.Role == UserRole.Guardian && u.Phone != null)
                .Select(u => new { u.Id, u.Phone })
                .ToListAsync();
            var sibling = sameAcademyGuardians.FirstOrDefault(u => Digits(u.Phone) == phone);
            if (sibling != null) return sibling.Id;
        }

        var email = hasEmail
            ? NormalizeEmail(dto.GuardianEmail!)
            : $"apoderado.{phone}.{Guid.NewGuid().ToString("N")[..6]}{NoEmailDomain}";
        var newGuardian = new User
        {
            AcademyId = academyId,
            FirstName = dto.GuardianFirstName!.Trim(),
            LastName = dto.GuardianLastName?.Trim() ?? "",
            Email = email,
            Phone = phone, // solo dígitos: así puede ingresar escribiendo su celular
            Role = UserRole.Guardian,
            Status = UserStatus.Active
        };
        var tempPassword = AuthService.AssignTemporaryPassword(newGuardian);
        _context.Users.Add(newGuardian);
        credentials.Add(new GeneratedCredentialDto
        {
            Role = "Apoderado",
            FullName = $"{newGuardian.FirstName} {newGuardian.LastName}".Trim(),
            Login = hasEmail ? email : phone,
            TemporaryPassword = tempPassword
        });
        return newGuardian.Id;
    }

    // ─────────────────────────────────────────────────────────────
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>Busca por email incluyendo usuarios eliminados: el índice único de email los incluye.</summary>
    private Task<User?> FindUserByEmailAsync(string normalizedEmail) =>
        _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

    private async Task ValidateHeadquarterAndCategoryAsync(Guid academyId, Guid headquarterId, Guid categoryId, bool requireSameHeadquarter = true)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.AcademyId == academyId);
        if (category == null) throw new Exception("La categoría seleccionada no es válida.");

        var hqOk = await _context.Headquarters.AnyAsync(h => h.Id == headquarterId && h.AcademyId == academyId);
        if (!hqOk) throw new Exception("La sede seleccionada no es válida.");

        if (requireSameHeadquarter && category.HeadquarterId != headquarterId)
            throw new Exception("La categoría seleccionada no pertenece a la sede indicada.");
    }

    /// <summary>
    /// Vincula al alumno con una cuenta de usuario Alumno de la misma academia o crea una nueva
    /// con contraseña temporal. Devuelve la credencial generada (o null si se reutilizó una cuenta).
    /// </summary>
    private async Task<GeneratedCredentialDto?> LinkOrCreateStudentUserAsync(Guid academyId, Student student, string? phone)
    {
        var email = student.Email!;
        var existing = await FindUserByEmailAsync(email);
        if (existing != null)
        {
            if (existing.IsDeleted || existing.AcademyId != academyId || existing.Role != UserRole.Student)
                throw new Exception($"El correo {email} ya está registrado en otra cuenta. Usa un correo distinto para el alumno.");

            var linkedToOther = await _context.Students.AnyAsync(s =>
                s.AcademyId == academyId && s.UserId == existing.Id && s.Id != student.Id);
            if (linkedToOther)
                throw new Exception("El correo electrónico ya pertenece a otro alumno de la academia.");

            student.UserId = existing.Id;
            return null;
        }

        var user = new User
        {
            AcademyId = academyId,
            FirstName = student.FirstName,
            LastName = student.LastName,
            Email = email,
            Phone = phone,
            Role = UserRole.Student,
            Status = UserStatus.Active
        };
        var temp = AuthService.AssignTemporaryPassword(user);
        _context.Users.Add(user);
        student.UserId = user.Id;

        return new GeneratedCredentialDto
        {
            Role = "Alumno",
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Login = email,
            TemporaryPassword = temp
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
        int age = 0;
        if (student.DateOfBirth.HasValue)
        {
            age = today.Year - student.DateOfBirth.Value.Year;
            if (student.DateOfBirth.Value.Date > today.AddYears(-age)) age--;
        }

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
    public async Task<StudentDto> UpdateStudentAsync(Guid academyId, Guid id, CreateStudentDto dto, bool canEditFinancials = true, bool canEditMedical = true)
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

        // En edición solo se valida que sede y categoría sean de la academia: hay registros antiguos
        // cuya categoría no coincide con la sede y no deben quedar bloqueados para editar.
        await ValidateHeadquarterAndCategoryAsync(academyId, dto.HeadquarterId, dto.CategoryId, requireSameHeadquarter: false);
        ValidateStudentInput(dto, isCreate: false);
        await EnsureNotDuplicateAsync(academyId, dto, id);
        _audit?.Add(academyId, student.IsActive && !dto.IsActive ? "students.withdraw" : !student.IsActive && dto.IsActive ? "students.reactivate" : "students.update",
            nameof(Student), student.Id, new { dto.FirstName, dto.LastName, dto.CategoryId, dto.IsActive, financialsApplied = canEditFinancials, outsideCategoryAge = await OutsideCategoryAgeAsync(dto.CategoryId, dto.DateOfBirth) });
        var credentials = new List<GeneratedCredentialDto>();

        student.FirstName = dto.FirstName;
        student.LastName = dto.LastName;
        student.DocumentNumber = dto.DocumentNumber;
        student.DateOfBirth = dto.DateOfBirth?.ToUniversalTime();
        student.HeadquarterId = dto.HeadquarterId;
        student.CategoryId = dto.CategoryId;
        student.IsActive = dto.IsActive;

        if (dto.EnrollmentDate.HasValue)
            student.EnrollmentDate = dto.EnrollmentDate.Value.ToUniversalTime();
            
        if (canEditFinancials)
            student.PaymentStartDate = dto.PaymentStartDate?.ToUniversalTime();
        student.WithdrawalDate = dto.WithdrawalDate?.ToUniversalTime();
        // Baja sin fecha: se registra hoy. Reactivación: se limpia la fecha de retiro pasada.
        var todayUtc = DateTime.SpecifyKind(AcademyClock.Today, DateTimeKind.Utc);
        if (!dto.IsActive && student.WithdrawalDate == null) student.WithdrawalDate = todayUtc;
        if (dto.IsActive && student.WithdrawalDate.HasValue && student.WithdrawalDate.Value.Date <= todayUtc.Date) student.WithdrawalDate = null;

        // Condiciones económicas: solo quien gestiona finanzas puede cambiarlas.
        if (canEditFinancials)
        {
            student.PreferentialFee = dto.PreferentialFee;
            if (ParsePlan(dto.BillingPlan) is { } plan)
            {
                student.BillingPlan = plan;
                student.PreferentialSessionFee = dto.PreferentialSessionFee;
            }
            student.IsGuest = dto.IsGuest;
            student.IsScholarship = dto.IsScholarship;
            student.ScholarshipPercentage = dto.ScholarshipPercentage;
        }

        var newEmail = string.IsNullOrWhiteSpace(dto.Email) ? null : NormalizeEmail(dto.Email);
        if (!string.Equals(student.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            if (student.UserId.HasValue && newEmail != null)
            {
                // El correo es el login del alumno: validar que no lo use otra cuenta.
                var owner = await FindUserByEmailAsync(newEmail);
                if (owner != null && owner.Id != student.UserId.Value)
                    throw new Exception($"El correo {newEmail} ya está registrado en otra cuenta.");
                var stUser = await _context.Users.FindAsync(student.UserId.Value);
                if (stUser != null) stUser.Email = newEmail;
            }
            // Si se borra el correo, la cuenta de usuario conserva su login actual
            // (no se puede dejar vacío: rompería el índice único de email).
            student.Email = newEmail;
        }

        student.Phone = dto.Phone;

        // Crear/vincular cuenta del alumno si ahora tiene email y celular
        if (!student.UserId.HasValue && newEmail != null && !string.IsNullOrWhiteSpace(dto.Phone))
        {
            var credential = await LinkOrCreateStudentUserAsync(academyId, student, dto.Phone);
            if (credential != null) credentials.Add(credential);
        }

        // Actualizar datos del apoderado vinculado
        var guardian = student.GuardianId.HasValue
            ? await _context.Users.FirstOrDefaultAsync(u => u.Id == student.GuardianId && u.AcademyId == academyId)
            : null;
        if (guardian != null)
        {
            if (!string.IsNullOrWhiteSpace(dto.GuardianFirstName)) guardian.FirstName = dto.GuardianFirstName.Trim();
            if (!string.IsNullOrWhiteSpace(dto.GuardianLastName)) guardian.LastName = dto.GuardianLastName.Trim();
            if (!string.IsNullOrWhiteSpace(dto.GuardianEmail))
            {
                var guardianEmail = NormalizeEmail(dto.GuardianEmail);
                if (!string.Equals(guardian.Email, guardianEmail, StringComparison.OrdinalIgnoreCase))
                {
                    var owner = await FindUserByEmailAsync(guardianEmail);
                    if (owner != null && owner.Id != guardian.Id)
                        throw new Exception($"El correo {guardianEmail} ya está registrado en otra cuenta.");
                    guardian.Email = guardianEmail;
                }
            }
            if (!string.IsNullOrWhiteSpace(dto.GuardianPhone)) guardian.Phone = Digits(dto.GuardianPhone); // solo dígitos: login por celular
        }
        else
        {
            // El alumno no tenía apoderado: si ahora se registran sus datos, se crea o vincula.
            var newGuardianId = await ResolveGuardianAsync(academyId, dto, credentials);
            if (newGuardianId.HasValue) student.GuardianId = newGuardianId;
        }

        var previousWeight = student.MedicalRecord?.WeightKg;
        var previousHeight = student.MedicalRecord?.HeightCm;

        // Sin permiso de salud la ficha no llega al formulario: guardarla en blanco la borraría.
        if (dto.MedicalRecord != null && canEditMedical)
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

            // Check if nutrition data changed
            if ((dto.MedicalRecord.WeightKg != previousWeight || dto.MedicalRecord.HeightCm != previousHeight) && 
                (dto.MedicalRecord.WeightKg.HasValue || dto.MedicalRecord.HeightCm.HasValue))
            {
                _context.StudentNutritionRecords.Add(new StudentNutritionRecord
                {
                    StudentId = student.Id,
                    WeightKg = dto.MedicalRecord.WeightKg,
                    HeightCm = dto.MedicalRecord.HeightCm,
                    BMI = dto.MedicalRecord.BMI,
                    Notes = dto.MedicalRecord.NutritionPlan,
                    RecordDate = DateTime.UtcNow
                });
            }
        }

        await EnsurePlanIsConfiguredAsync(student);
        await _context.SaveChangesAsync();

        return new StudentDto
        {
            Id = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName,
            BillingPlan = student.BillingPlan.ToString(),
            PreferentialSessionFee = student.PreferentialSessionFee,
            GeneratedCredentials = credentials.Count > 0 ? credentials : null
        };
    }

    private static StudentBillingPlan? ParsePlan(string? value) =>
        Enum.TryParse<StudentBillingPlan>(value, true, out var p) && Enum.IsDefined(p) ? p : null;

    /// <summary>Por sesión necesita precio por sesión; paquete necesita el paquete en la categoría.</summary>
    private async Task EnsurePlanIsConfiguredAsync(Student student)
    {
        if (student.BillingPlan == StudentBillingPlan.Monthly) return;
        var category = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == student.CategoryId)
            ?? throw new Exception("Categoría no encontrada.");
        if ((student.PreferentialSessionFee ?? category.SessionFee) <= 0)
            throw new Exception("Para cobrar por sesión o paquete, configura el precio por sesión en la categoría o en el alumno.");
        if (student.BillingPlan == StudentBillingPlan.Package && !(category.PackageSessions > 0 && category.PackagePrice > 0))
            throw new Exception("La categoría no tiene paquete configurado (sesiones y precio).");
    }

    /// <summary>Excepción de edad: se permite (decisión del entrenador) pero queda en la auditoría.</summary>
    private async Task<bool> OutsideCategoryAgeAsync(Guid categoryId, DateTime? birthDate)
    {
        var category = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId);
        return category != null && !category.FitsBirthDate(birthDate, AcademyClock.Today);
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
            Age = s.DateOfBirth.HasValue ? today.Year - s.DateOfBirth.Value.Year - (s.DateOfBirth.Value.Date > today.AddYears(-(today.Year - s.DateOfBirth.Value.Year)) ? 1 : 0) : 0,
            HeadquarterId = s.HeadquarterId,
            HeadquarterName = s.Headquarter?.Name ?? "",
            CategoryId = s.CategoryId,
            CategoryName = s.Category?.Name ?? "",
            Email = s.Email,
            GuardianId = s.GuardianId,
            GuardianName = s.Guardian != null ? s.Guardian.FirstName + " " + s.Guardian.LastName : "", GuardianFirstName = s.Guardian != null ? s.Guardian.FirstName : "", GuardianLastName = s.Guardian != null ? s.Guardian.LastName : "",
            GuardianPhone = s.Guardian?.Phone ?? "",
            GuardianEmail = IsPlaceholderEmail(s.Guardian?.Email) ? "" : s.Guardian?.Email ?? "",
            IsActive = s.IsActive,
            EnrollmentDate = s.EnrollmentDate,
            PaymentStartDate = s.PaymentStartDate,
            WithdrawalDate = s.WithdrawalDate,
            PreferentialFee = s.PreferentialFee,
            BillingPlan = s.BillingPlan.ToString(),
            PreferentialSessionFee = s.PreferentialSessionFee,
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

        var dtos = records.Select(r => new StudentNutritionRecordDto
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

        if (dtos.Count == 0)
        {
            var medRecord = await _context.StudentMedicalRecords.FirstOrDefaultAsync(m => m.StudentId == studentId);
            if (medRecord != null && (medRecord.WeightKg.HasValue || medRecord.HeightCm.HasValue))
            {
                dtos.Add(new StudentNutritionRecordDto
                {
                    Id = Guid.Empty,
                    StudentId = studentId,
                    WeightKg = medRecord.WeightKg,
                    HeightCm = medRecord.HeightCm,
                    BMI = medRecord.BMI,
                    Notes = medRecord.NutritionPlan,
                    RecordDate = studentId == Guid.Empty ? DateTime.UtcNow : DateTime.UtcNow, // Fallback
                    CreatedAt = DateTime.UtcNow,
                    RegisteredByName = "Registro Inicial"
                });
            }
        }

        return dtos;
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
