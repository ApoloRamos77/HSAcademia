using HSAcademia.Application.DTOs.Finances;
using HSAcademia.Domain.Entities;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Comprobantes de pago enviados por las familias. Reemplaza el antiguo "pago desde la app"
/// (que marcaba deudas como pagadas sin cobro real): ahora la academia valida cada comprobante.
/// </summary>
public class PaymentProofService
{
    private readonly AppDbContext _db;
    private readonly FinancesService _finances;
    private readonly AuditService _audit;

    public PaymentProofService(AppDbContext db, FinancesService finances, AuditService audit)
    {
        _db = db;
        _finances = finances;
        _audit = audit;
    }

    // ── Familia ────────────────────────────────────────────────
    public async Task<PaymentProofDto> SubmitAsync(Guid academyId, Guid userId, Guid paymentRecordId,
        decimal amount, string? method, string? operationNumber, DateTime? paidOn, string? notes, string voucherUrl)
    {
        var debt = await _db.PaymentRecords
            .Include(p => p.Student)
            .FirstOrDefaultAsync(p => p.Id == paymentRecordId && p.AcademyId == academyId &&
                (p.Student.GuardianId == userId || p.Student.UserId == userId))
            ?? throw new KeyNotFoundException("Deuda no encontrada.");

        if (debt.IsPaid) throw new Exception("Esta deuda ya está pagada.");
        var pending = debt.Amount - debt.AmountPaid;
        if (amount <= 0) throw new Exception("El monto debe ser mayor a cero.");
        if (amount > pending + 0.01m) throw new Exception($"El monto supera el saldo pendiente (S/ {pending:N2}).");

        if (await _db.PaymentProofs.AnyAsync(p => p.PaymentRecordId == paymentRecordId && p.Status == PaymentProofStatus.Pending))
            throw new Exception("Ya enviaste un comprobante para esta deuda que está pendiente de validación.");

        var day = (paidOn ?? AcademyClock.Today).Date;
        if (day > AcademyClock.Today) throw new Exception("La fecha de pago no puede ser futura.");
        // Pago fuera de fecha: se acepta; al validarlo, administración decide si corresponde mora.

        var proof = new PaymentProof
        {
            AcademyId = academyId,
            PaymentRecordId = debt.Id,
            StudentId = debt.StudentId,
            SubmittedByUserId = userId,
            Amount = Math.Round(amount, 2),
            Method = Enum.TryParse<PaymentMethod>(method, true, out var m) && m != PaymentMethod.Cash && m != PaymentMethod.Mixed ? m : PaymentMethod.Yape,
            OperationNumber = operationNumber?.Trim(),
            VoucherUrl = voucherUrl,
            PaidOn = day,
            Notes = notes?.Trim()
        };
        _db.PaymentProofs.Add(proof);
        _audit.Add(academyId, "finances.proof.submit", nameof(PaymentProof), proof.Id,
            new { proof.PaymentRecordId, proof.Amount, method = proof.Method.ToString(), proof.OperationNumber });
        await _db.SaveChangesAsync();

        return (await QueryDtos(academyId).FirstAsync(p => p.Id == proof.Id));
    }

    public Task<List<PaymentProofDto>> GetMineAsync(Guid academyId, Guid userId) =>
        QueryDtos(academyId, p => p.Student.GuardianId == userId || p.Student.UserId == userId)
            .OrderByDescending(p => p.CreatedAt).Take(100).ToListAsync();

    // ── Academia ───────────────────────────────────────────────
    public Task<List<PaymentProofDto>> GetForReviewAsync(Guid academyId, PaymentProofStatus? status)
    {
        var q = status.HasValue
            ? QueryDtos(academyId, p => p.Status == status.Value)
            : QueryDtos(academyId);
        return q.OrderBy(p => p.Status == "Pending" ? 0 : 1).ThenByDescending(p => p.CreatedAt).Take(300).ToListAsync();
    }

    public Task<int> CountPendingAsync(Guid academyId) =>
        _db.PaymentProofs.CountAsync(p => p.AcademyId == academyId && p.Status == PaymentProofStatus.Pending);

    public async Task<PaymentProofDto> ApproveAsync(Guid academyId, Guid reviewerId, Guid proofId, ApprovePaymentProofDto dto)
    {
        var proof = await _db.PaymentProofs.AsNoTracking()
            .Include(p => p.PaymentRecord)
            .FirstOrDefaultAsync(p => p.Id == proofId && p.AcademyId == academyId)
            ?? throw new KeyNotFoundException("Comprobante no encontrado.");
        if (proof.Status != PaymentProofStatus.Pending) throw new Exception("Este comprobante ya fue revisado.");

        var pending = proof.PaymentRecord.Amount - proof.PaymentRecord.AmountPaid;
        var amount = Math.Min(dto.Amount ?? proof.Amount, pending);
        if (amount <= 0) throw new Exception("La deuda ya no tiene saldo pendiente. Rechaza el comprobante indicando el motivo.");

        await _db.InTransactionAsync(async () =>
        {
            var record = await _db.PaymentRecords.FirstAsync(p => p.Id == proof.PaymentRecordId);
            if (FinancesService.RequiresRegularization(record))
            {
                if (proof.PaidOn.Date <= record.DueDate.Date)
                {
                    // Pagado a tiempo pero validado después del vencimiento: sin mora.
                    record.RegularizedAt = DateTime.UtcNow;
                    record.RegularizedByUserId = reviewerId;
                    record.RegularizationNote = $"Pagado a tiempo (comprobante del {proof.PaidOn:dd/MM/yyyy}); validado después del vencimiento.";
                }
                else
                {
                    // Pagado fuera de fecha: decisión de mora obligatoria. La mora queda como deuda pendiente.
                    if (dto.Regularization == null)
                        throw new Exception($"El pago se hizo el {proof.PaidOn:dd/MM/yyyy}, después del vencimiento ({record.DueDate:dd/MM/yyyy}): indica si se cobra mora o no y la observación.");
                    await _finances.ApplyRegularizationAsync(academyId, record, dto.Regularization, reviewerId);
                }
                await _db.SaveChangesAsync();
            }

            // Registra el abono (número de recibo, cierre de periodo, concurrencia, auditoría).
            var result = await _finances.RegisterPaymentAsync(academyId, new RegisterPaymentDto
            {
                PaymentRecordId = proof.PaymentRecordId,
                AmountPaid = amount,
                Method = proof.Method.ToString(),
                OperationNumber = proof.OperationNumber,
                VoucherUrl = proof.VoucherUrl,
                Channel = "comprobante",
                Notes = string.IsNullOrWhiteSpace(dto.Notes)
                    ? $"Validado desde comprobante del {proof.PaidOn:dd/MM/yyyy}"
                    : dto.Notes
            });
            var receipt = result.Installments.OrderByDescending(i => i.PaidAt).FirstOrDefault()?.ReceiptNumber;

            var tracked = await _db.PaymentProofs.FirstAsync(p => p.Id == proofId);
            if (tracked.Status != PaymentProofStatus.Pending) throw new Exception("Este comprobante ya fue revisado.");
            tracked.Status = PaymentProofStatus.Approved;
            tracked.ReviewedByUserId = reviewerId;
            tracked.ReviewedAt = DateTime.UtcNow;
            tracked.ReceiptNumber = receipt;
            tracked.UpdatedAt = DateTime.UtcNow;
            _audit.Add(academyId, "finances.proof.approve", nameof(PaymentProof), proofId, new { amount, receipt });
            await _db.SaveChangesAsync();
            return true;
        });

        return await QueryDtos(academyId).FirstAsync(p => p.Id == proofId);
    }

    public async Task<PaymentProofDto> RejectAsync(Guid academyId, Guid reviewerId, Guid proofId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new Exception("Indica el motivo del rechazo para informar a la familia.");

        var proof = await _db.PaymentProofs.FirstOrDefaultAsync(p => p.Id == proofId && p.AcademyId == academyId)
            ?? throw new KeyNotFoundException("Comprobante no encontrado.");
        if (proof.Status != PaymentProofStatus.Pending) throw new Exception("Este comprobante ya fue revisado.");

        proof.Status = PaymentProofStatus.Rejected;
        proof.RejectionReason = reason.Trim();
        proof.ReviewedByUserId = reviewerId;
        proof.ReviewedAt = DateTime.UtcNow;
        proof.UpdatedAt = DateTime.UtcNow;
        _audit.Add(academyId, "finances.proof.reject", nameof(PaymentProof), proofId, new { reason = proof.RejectionReason });
        await _db.SaveChangesAsync();

        return await QueryDtos(academyId).FirstAsync(p => p.Id == proofId);
    }

    private IQueryable<PaymentProofDto> QueryDtos(Guid academyId,
        System.Linq.Expressions.Expression<Func<PaymentProof, bool>>? filter = null)
    {
        var q = _db.PaymentProofs.AsNoTracking().Where(p => p.AcademyId == academyId);
        if (filter != null) q = q.Where(filter);
        return q.Select(p => new PaymentProofDto
        {
            Id = p.Id,
            PaymentRecordId = p.PaymentRecordId,
            DebtDescription = p.PaymentRecord.Description,
            DebtPending = p.PaymentRecord.Amount - p.PaymentRecord.AmountPaid,
            DebtDueDate = p.PaymentRecord.DueDate,
            RequiresRegularization = p.Status == PaymentProofStatus.Pending && p.PaymentRecord.Type == PaymentType.MonthlyFee &&
                                     !p.PaymentRecord.IsPaid && p.PaymentRecord.RegularizedAt == null &&
                                     p.PaidOn.Date > p.PaymentRecord.DueDate.Date,
            StudentId = p.StudentId,
            StudentName = p.Student.FirstName + " " + p.Student.LastName,
            SubmittedByName = p.SubmittedBy.FirstName + " " + p.SubmittedBy.LastName,
            Amount = p.Amount,
            Method = p.Method.ToString(),
            OperationNumber = p.OperationNumber,
            VoucherUrl = p.VoucherUrl,
            PaidOn = p.PaidOn,
            Notes = p.Notes,
            Status = p.Status == PaymentProofStatus.Pending ? "Pending" : p.Status == PaymentProofStatus.Approved ? "Approved" : "Rejected",
            CreatedAt = p.CreatedAt,
            ReviewedAt = p.ReviewedAt,
            RejectionReason = p.RejectionReason,
            ReceiptNumber = p.ReceiptNumber
        });
    }
}
