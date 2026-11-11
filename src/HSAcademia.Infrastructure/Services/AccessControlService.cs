using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Verificaciones de pertenencia de recursos para usuarios finales (Alumno / Apoderado).
/// Evita que un usuario consulte o modifique datos de alumnos que no le corresponden (IDOR).
/// </summary>
public class AccessControlService
{
    private readonly AppDbContext _db;

    public AccessControlService(AppDbContext db) => _db = db;

    /// <summary>True si el usuario es el propio alumno o su apoderado.</summary>
    public Task<bool> UserOwnsStudentAsync(Guid academyId, Guid userId, Guid studentId) =>
        _db.Students.AsNoTracking().AnyAsync(s =>
            s.Id == studentId &&
            s.AcademyId == academyId &&
            (s.UserId == userId || s.GuardianId == userId));

    /// <summary>El personal no abre fichas de alumnos retirados. Si no existe, el servicio responde 404.</summary>
    public async Task<bool> StudentIsActiveAsync(Guid academyId, Guid studentId) =>
        !await _db.Students.AsNoTracking().AnyAsync(s => s.Id == studentId && s.AcademyId == academyId && !s.IsActive);

    /// <summary>True si la convocatoria pertenece a un alumno del usuario.</summary>
    public Task<bool> UserOwnsEventCallAsync(Guid academyId, Guid userId, Guid eventCallId) =>
        _db.EventCalls.AsNoTracking().AnyAsync(c =>
            c.Id == eventCallId &&
            c.AcademyId == academyId &&
            (c.Student.UserId == userId || c.Student.GuardianId == userId));

    public async Task<bool> StudentInScopeAsync(Guid academyId, Guid studentId, AccessScope scope)
    {
        if (scope.IsAcademyWide) return true;
        var student = await _db.Students.AsNoTracking()
            .Where(s => s.Id == studentId && s.AcademyId == academyId)
            .Select(s => new { s.HeadquarterId })
            .FirstOrDefaultAsync();
        return student == null || scope.CoversHeadquarter(student.HeadquarterId);
    }

    public async Task<bool> DebtInScopeAsync(Guid academyId, Guid debtId, AccessScope scope)
    {
        if (scope.IsAcademyWide) return true;
        var debt = await _db.PaymentRecords.AsNoTracking()
            .Where(p => p.Id == debtId && p.AcademyId == academyId)
            .Select(p => new { p.Student.HeadquarterId })
            .FirstOrDefaultAsync();
        return debt == null || scope.CoversHeadquarter(debt.HeadquarterId);
    }

    public async Task<bool> ProofInScopeAsync(Guid academyId, Guid proofId, AccessScope scope)
    {
        if (scope.IsAcademyWide) return true;
        var proof = await _db.PaymentProofs.AsNoTracking()
            .Where(p => p.Id == proofId && p.AcademyId == academyId)
            .Select(p => new { p.Student.HeadquarterId })
            .FirstOrDefaultAsync();
        return proof == null || scope.CoversHeadquarter(proof.HeadquarterId);
    }

    /// <summary>Un recibo sin alumnos (venta al público) no pertenece a ninguna sede.</summary>
    public async Task<bool> ReceiptInScopeAsync(Guid academyId, Guid receiptId, AccessScope scope)
    {
        if (scope.IsAcademyWide) return true;
        var studentIds = await _db.Receipts.AsNoTracking()
            .Where(r => r.Id == receiptId && r.AcademyId == academyId)
            .SelectMany(r => r.Lines.Where(l => l.StudentId != null).Select(l => l.StudentId!.Value))
            .ToListAsync();
        if (studentIds.Count == 0) return true;
        var allowed = scope.Headquarters!;
        return await _db.Students.AsNoTracking().AnyAsync(s => studentIds.Contains(s.Id) && allowed.Contains(s.HeadquarterId));
    }

    /// <summary>True si la categoría está asignada al miembro del personal.</summary>
    public Task<bool> StaffCanAccessCategoryAsync(Guid academyId, Guid userId, Guid categoryId) =>
        _db.Users.AsNoTracking().AnyAsync(u =>
            u.Id == userId &&
            u.AcademyId == academyId &&
            u.AssignedCategories.Any(c => c.Id == categoryId));

    /// <summary>True si el alumno pertenece a una categoría asignada. Si no existe, el servicio responde 404.</summary>
    public async Task<bool> StaffCanAccessStudentAsync(Guid academyId, Guid userId, Guid studentId)
    {
        var student = await _db.Students.AsNoTracking()
            .Where(s => s.Id == studentId && s.AcademyId == academyId)
            .Select(s => new { s.CategoryId })
            .FirstOrDefaultAsync();
        return student == null || await StaffCanAccessCategoryAsync(academyId, userId, student.CategoryId);
    }

    /// <summary>True si dicta la sesión o tiene asignada alguna de sus categorías. Si no existe, el servicio responde 404.</summary>
    public async Task<bool> StaffCanAccessEventAsync(Guid academyId, Guid userId, Guid eventId)
    {
        var ev = await _db.Events.AsNoTracking()
            .Where(e => e.Id == eventId && e.AcademyId == academyId)
            .Select(e => new { e.TeacherId, e.CategoryId, e.CategoryIds })
            .FirstOrDefaultAsync();
        if (ev == null || ev.TeacherId == userId) return true;

        var categories = new List<Guid>(ev.CategoryIds ?? new List<Guid>());
        if (ev.CategoryId.HasValue) categories.Add(ev.CategoryId.Value);
        return await _db.Users.AsNoTracking().AnyAsync(u =>
            u.Id == userId &&
            u.AcademyId == academyId &&
            u.AssignedCategories.Any(c => categories.Contains(c.Id)));
    }
}
