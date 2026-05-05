using System;

namespace HSAcademia.Domain.Entities;

public class AcademyFinancialConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public virtual Academy Academy { get; set; } = null!;

    // El día del mes que se cobra la mensualidad (ej. 5 de cada mes)
    public int DefaultPaymentDay { get; set; } = 5;

    // Número de recibo actual, inicia en 43 para que el siguiente sea 44
    public int CurrentReceiptNumber { get; set; } = 43;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
