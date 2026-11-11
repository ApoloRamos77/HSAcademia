using HSAcademia.Application.DTOs.Student;
using HSAcademia.Domain.Entities;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>Evaluaciones deportivas periódicas de los alumnos.</summary>
public class EvaluationService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public EvaluationService(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public Task<List<EvaluationDto>> GetByStudentAsync(Guid academyId, Guid studentId, bool onlyVisibleToFamily)
    {
        var q = _db.StudentEvaluations.AsNoTracking()
            .Where(e => e.AcademyId == academyId && e.StudentId == studentId);
        if (onlyVisibleToFamily) q = q.Where(e => e.IsVisibleToFamily);
        return Project(q.OrderByDescending(e => e.EvaluationDate).ThenByDescending(e => e.CreatedAt)).ToListAsync();
    }

    public async Task<EvaluationDto> CreateAsync(Guid academyId, Guid evaluatorId, bool isAdmin, Guid studentId, SaveEvaluationDto dto)
    {
        Validate(dto);
        await EnsureCanEvaluateAsync(academyId, evaluatorId, isAdmin, studentId);

        var entity = new StudentEvaluation
        {
            AcademyId = academyId,
            StudentId = studentId,
            EvaluatorUserId = evaluatorId,
        };
        Apply(entity, dto);
        _db.StudentEvaluations.Add(entity);
        _audit.Add(academyId, "students.evaluation.create", nameof(StudentEvaluation), entity.Id, new { studentId, dto.Period });
        await _db.SaveChangesAsync();
        return await Project(_db.StudentEvaluations.Where(e => e.Id == entity.Id)).FirstAsync();
    }

    public async Task<EvaluationDto> UpdateAsync(Guid academyId, Guid userId, bool isAdmin, Guid evaluationId, SaveEvaluationDto dto)
    {
        Validate(dto);
        var entity = await _db.StudentEvaluations.FirstOrDefaultAsync(e => e.Id == evaluationId && e.AcademyId == academyId)
            ?? throw new KeyNotFoundException("Evaluación no encontrada.");
        if (!isAdmin && entity.EvaluatorUserId != userId)
            throw new Exception("Solo el autor de la evaluación o un administrador puede editarla.");

        Apply(entity, dto);
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await Project(_db.StudentEvaluations.Where(e => e.Id == entity.Id)).FirstAsync();
    }

    public async Task DeleteAsync(Guid academyId, Guid userId, bool isAdmin, Guid evaluationId)
    {
        var entity = await _db.StudentEvaluations.FirstOrDefaultAsync(e => e.Id == evaluationId && e.AcademyId == academyId)
            ?? throw new KeyNotFoundException("Evaluación no encontrada.");
        if (!isAdmin && entity.EvaluatorUserId != userId)
            throw new Exception("Solo el autor de la evaluación o un administrador puede eliminarla.");
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        _audit.Add(academyId, "students.evaluation.delete", nameof(StudentEvaluation), entity.Id, new { entity.StudentId, entity.Period });
        await _db.SaveChangesAsync();
    }

    /// <summary>Un entrenador solo evalúa alumnos de sus categorías asignadas.</summary>
    private async Task EnsureCanEvaluateAsync(Guid academyId, Guid userId, bool isAdmin, Guid studentId)
    {
        var student = await _db.Students.AsNoTracking()
            .Where(s => s.Id == studentId && s.AcademyId == academyId)
            .Select(s => new { s.CategoryId, s.IsActive })
            .FirstOrDefaultAsync() ?? throw new KeyNotFoundException("Alumno no encontrado.");
        if (isAdmin) return;
        if (!student.IsActive) throw new Exception("El alumno está retirado: solo un administrador puede evaluarlo.");

        var assigned = await _db.Users.Where(u => u.Id == userId)
            .SelectMany(u => u.AssignedCategories.Select(c => c.Id)).ToListAsync();
        if (!assigned.Contains(student.CategoryId))
            throw new Exception("Solo puedes evaluar alumnos de tus categorías asignadas.");
    }

    private static void Validate(SaveEvaluationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Period)) throw new Exception("Indica el periodo de la evaluación (ej. 2026 - Trimestre 1).");
        foreach (var v in new[] { dto.Technical, dto.Tactical, dto.Physical, dto.Attitude })
            if (v < 1 || v > 5) throw new Exception("Cada dimensión se califica de 1 a 5.");
        if (dto.EvaluationDate.HasValue && dto.EvaluationDate.Value.Date > AcademyClock.Today)
            throw new Exception("La fecha de evaluación no puede ser futura.");
    }

    private static void Apply(StudentEvaluation e, SaveEvaluationDto dto)
    {
        e.Period = dto.Period.Trim();
        e.EvaluationDate = (dto.EvaluationDate ?? AcademyClock.Today).Date;
        e.Technical = dto.Technical;
        e.Tactical = dto.Tactical;
        e.Physical = dto.Physical;
        e.Attitude = dto.Attitude;
        e.Strengths = dto.Strengths?.Trim();
        e.ToImprove = dto.ToImprove?.Trim();
        e.Goals = dto.Goals?.Trim();
        e.Comments = dto.Comments?.Trim();
        e.IsVisibleToFamily = dto.IsVisibleToFamily;
    }

    private static IQueryable<EvaluationDto> Project(IQueryable<StudentEvaluation> q) => q.Select(e => new EvaluationDto
    {
        Id = e.Id,
        StudentId = e.StudentId,
        StudentName = e.Student.FirstName + " " + e.Student.LastName,
        Period = e.Period,
        EvaluationDate = e.EvaluationDate,
        Technical = e.Technical,
        Tactical = e.Tactical,
        Physical = e.Physical,
        Attitude = e.Attitude,
        Average = Math.Round((e.Technical + e.Tactical + e.Physical + e.Attitude) / 4.0m, 1),
        Strengths = e.Strengths,
        ToImprove = e.ToImprove,
        Goals = e.Goals,
        Comments = e.Comments,
        IsVisibleToFamily = e.IsVisibleToFamily,
        EvaluatorName = e.Evaluator.FirstName + " " + e.Evaluator.LastName,
        CreatedAt = e.CreatedAt
    });
}
