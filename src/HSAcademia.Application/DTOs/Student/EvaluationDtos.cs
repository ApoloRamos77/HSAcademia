namespace HSAcademia.Application.DTOs.Student;

public class EvaluationDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public DateTime EvaluationDate { get; set; }
    public int Technical { get; set; }
    public int Tactical { get; set; }
    public int Physical { get; set; }
    public int Attitude { get; set; }
    public decimal Average { get; set; }
    public string? Strengths { get; set; }
    public string? ToImprove { get; set; }
    public string? Goals { get; set; }
    public string? Comments { get; set; }
    public bool IsVisibleToFamily { get; set; }
    public string EvaluatorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SaveEvaluationDto
{
    public string Period { get; set; } = string.Empty;
    public DateTime? EvaluationDate { get; set; }
    public int Technical { get; set; }
    public int Tactical { get; set; }
    public int Physical { get; set; }
    public int Attitude { get; set; }
    public string? Strengths { get; set; }
    public string? ToImprove { get; set; }
    public string? Goals { get; set; }
    public string? Comments { get; set; }
    public bool IsVisibleToFamily { get; set; } = true;
}
