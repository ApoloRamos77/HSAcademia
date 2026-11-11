namespace HSAcademia.Domain.Enums;

/// <summary>Esquema de pago del personal (ficha laboral).</summary>
public enum StaffPaymentType
{
    /// <summary>Sueldo fijo mensual (PaymentRate), repartido según la frecuencia de pago.</summary>
    Monthly = 0,
    /// <summary>Por sesión dictada, según tarifario (o PaymentRate si no tiene).</summary>
    PerSession = 1,
    /// <summary>Por hora dictada, según tarifario (o PaymentRate si no tiene).</summary>
    PerHour = 2,
    /// <summary>Sueldo fijo (PaymentRate) más las horas dictadas según tarifario.</summary>
    Mixed = 3
}

/// <summary>Cada cuánto se le paga: define los periodos de liquidación.</summary>
public enum PayFrequency
{
    Monthly = 0,
    /// <summary>Del 1 al 15 y del 16 a fin de mes.</summary>
    Biweekly = 1,
    /// <summary>De lunes a domingo.</summary>
    Weekly = 2,
    Daily = 3
}

/// <summary>Unidad que cobra el tarifario.</summary>
public enum PayUnit
{
    Hour = 0,
    Session = 1
}

public enum StaffPaymentLineKind
{
    Bonus = 0,
    Deduction = 1,
    /// <summary>Adelanto entregado antes: se descuenta.</summary>
    Advance = 2
}
