namespace HSAcademia.Domain.Entities;

/// <summary>
/// Evaluación deportiva periódica de un alumno (ej. trimestral). Escalas de 1 a 5 por dimensión,
/// más observaciones cualitativas y objetivos para el siguiente periodo.
/// </summary>
public class StudentEvaluation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }

    public Guid StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public Guid EvaluatorUserId { get; set; }
    public virtual User Evaluator { get; set; } = null!;

    /// <summary>Etiqueta del periodo, ej. "2026 - Trimestre 1".</summary>
    public string Period { get; set; } = string.Empty;
    public DateTime EvaluationDate { get; set; }

    // Dimensiones (1 = en inicio · 5 = destacado)
    public int Technical { get; set; }
    public int Tactical { get; set; }
    public int Physical { get; set; }
    public int Attitude { get; set; }

    public string? Strengths { get; set; }
    public string? ToImprove { get; set; }
    public string? Goals { get; set; }
    public string? Comments { get; set; }

    /// <summary>Si es false, solo la ve el personal (borrador / uso interno).</summary>
    public bool IsVisibleToFamily { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
