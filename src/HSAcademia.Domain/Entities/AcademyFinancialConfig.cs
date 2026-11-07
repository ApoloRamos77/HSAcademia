using System;

namespace HSAcademia.Domain.Entities;

public class AcademyFinancialConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public virtual Academy Academy { get; set; } = null!;

    // El día del mes que se cobra la mensualidad (ej. 5 de cada mes)
    public int DefaultPaymentDay { get; set; } = 5;

    /// <summary>Mora sugerida al regularizar un pago vencido (administración puede cambiarla o no cobrarla).</summary>
    public decimal LateFeeAmount { get; set; } = 0m;

    // Último número de recibo emitido (el siguiente será +1). Las academias nuevas empiezan en 0;
    // las existentes conservan su valor actual. Protegido como token de concurrencia.
    public int CurrentReceiptNumber { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
