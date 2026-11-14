namespace HSAcademia.Domain.Entities;

/// <summary>Cuándo se cobra un concepto del torneo.</summary>
public enum TournamentFeeFrequency
{
    /// <summary>Una sola vez por jugador del plantel (inscripción, camiseta, seguro).</summary>
    Once = 1,
    /// <summary>En cada partido, a quien jugó (arbitraje, cancha, pasaje).</summary>
    PerMatch = 2
}

/// <summary>Cómo se calcula el monto de cada jugador.</summary>
public enum TournamentFeeAmountMode
{
    /// <summary>El monto es por jugador.</summary>
    PerPlayer = 1,
    /// <summary>El monto es un costo total que se reparte entre quienes pagan (redondeado hacia arriba al céntimo).</summary>
    SplitTotal = 2
}

public enum TournamentChargeStatus
{
    /// <summary>Cobrado: tiene su deuda en Cobranzas.</summary>
    Charged = 1,
    /// <summary>Confirmó y no asistió: espera la decisión del administrador.</summary>
    Review = 2,
    /// <summary>No se cobra (decisión del administrador o deuda anulada en Cobranzas).</summary>
    Waived = 3,
    /// <summary>Exonerado por beca o invitado.</summary>
    Exempt = 4
}

/// <summary>Concepto que se cobra a las familias por un torneo (inscripción, arbitraje, cancha, pasaje…).</summary>
public class TournamentFee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public Guid TournamentId { get; set; }
    /// <summary>Equipo al que aplica; null = todos los equipos del torneo.</summary>
    public Guid? TeamId { get; set; }

    public string Name { get; set; } = string.Empty;
    public TournamentFeeFrequency Frequency { get; set; } = TournamentFeeFrequency.PerMatch;
    public TournamentFeeAmountMode AmountMode { get; set; } = TournamentFeeAmountMode.PerPlayer;
    /// <summary>Monto por jugador o costo total a repartir, según AmountMode.</summary>
    public decimal Amount { get; set; }
    /// <summary>Vencimiento de los cobros de una sola vez (null = el día en que se generan).</summary>
    public DateTime? DueDate { get; set; }
    /// <summary>Becados e invitados no pagan este concepto.</summary>
    public bool ExemptScholarship { get; set; } = true;
    /// <summary>Inactivo: deja de generar cobros y anula los que no tienen pagos.</summary>
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public virtual Tournament Tournament { get; set; } = null!;
    public virtual TournamentTeam? Team { get; set; }
}

/// <summary>
/// Lo que corresponde a un alumno por un concepto (y un partido, si es por partido).
/// Hace de libro: evita cobrar dos veces y guarda las decisiones del administrador.
/// </summary>
public class TournamentCharge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public Guid FeeId { get; set; }
    /// <summary>Partido (solo en conceptos por partido).</summary>
    public Guid? EventId { get; set; }
    public Guid StudentId { get; set; }

    public TournamentChargeStatus Status { get; set; }
    /// <summary>Deuda creada en Cobranzas (solo en Charged).</summary>
    public Guid? PaymentRecordId { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Decisión manual (cobrar o no a quien confirmó y no asistió).</summary>
    public DateTime? DecidedAt { get; set; }
    public string? DecidedByName { get; set; }
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual TournamentFee Fee { get; set; } = null!;
    public virtual Student Student { get; set; } = null!;
    public virtual PaymentRecord? PaymentRecord { get; set; }
}
