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

    /// <summary>True si la convocatoria pertenece a un alumno del usuario.</summary>
    public Task<bool> UserOwnsEventCallAsync(Guid academyId, Guid userId, Guid eventCallId) =>
        _db.EventCalls.AsNoTracking().AnyAsync(c =>
            c.Id == eventCallId &&
            c.AcademyId == academyId &&
            (c.Student.UserId == userId || c.Student.GuardianId == userId));
}
