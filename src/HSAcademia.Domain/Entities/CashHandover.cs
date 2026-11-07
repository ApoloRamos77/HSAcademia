using System;
using System.Collections.Generic;

namespace HSAcademia.Domain.Entities;

public enum CashHandoverStatus
{
    /// <summary>El cobrador declaró lo recaudado y está por entregar el efectivo.</summary>
    Pending = 1,
    /// <summary>Finanzas recibió y contó el efectivo.</summary>
    Received = 2
}

/// <summary>
/// Rendición de caja de un cobrador (coordinador, cajero o administrador): agrupa sus recibos aún no rendidos.
/// El efectivo esperado sale de los recibos; se compara con lo declarado por el cobrador y con lo recibido.
/// </summary>
public class CashHandover
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }

    public Guid CollectorUserId { get; set; }
    public string CollectorName { get; set; } = string.Empty;
    public DateTime DeclaredAt { get; set; } = DateTime.UtcNow;

    public int ReceiptsCount { get; set; }
    /// <summary>Efectivo según los recibos.</summary>
    public decimal ExpectedCash { get; set; }
    /// <summary>Yape, Plin, transferencias y tarjeta (no se entregan físicamente; se concilian con el banco).</summary>
    public decimal DigitalTotal { get; set; }
    /// <summary>Efectivo que el cobrador dice entregar.</summary>
    public decimal DeclaredCash { get; set; }
    public string? Notes { get; set; }

    public CashHandoverStatus Status { get; set; } = CashHandoverStatus.Pending;
    public Guid? ReceivedByUserId { get; set; }
    public string? ReceivedByName { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public decimal? ReceivedCash { get; set; }
    public string? ReceivedNotes { get; set; }

    public virtual ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
}
