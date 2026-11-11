namespace HSAcademia.Domain.Enums;

public enum StaffPaymentStatus
{
    /// <summary>Aprobada, por pagar.</summary>
    Pending = 0,
    Paid = 1,
    /// <summary>Borrador: se puede recalcular y ajustar; el personal no la ve.</summary>
    Draft = 2
}
