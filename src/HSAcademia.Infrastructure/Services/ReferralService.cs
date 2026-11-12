using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HSAcademia.Domain.Entities;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Programa de referidos.
/// - Campañas con vigencia y montos configurables (una vigente a la vez). Los montos se congelan al registrar.
/// - El referido (alumno nuevo) recibe su beneficio, si la campaña lo tiene, en su primer cobro.
/// - El referente lo gana cuando el referido paga su primer cobro, y se le aplica en su siguiente mensualidad
///   (o liquidación de sesiones). Si el beneficio supera la deuda, el saldo pasa a la siguiente.
/// - Los beneficios se aplican como descuento sobre la deuda: la familia los ve en la app, en caja y en el recibo.
/// - Todo es recalculable (<see cref="SyncAsync"/>): si un pago se anula o una deuda se regenera, el beneficio vuelve
///   a quedar disponible y se aplica de nuevo; nunca se toca lo ya pagado.
/// </summary>
public class ReferralService
{
    /// <summary>Un exalumno vuelve a contar como referido solo si no pagó nada en este plazo.</summary>
    public const int ReturningStudentDays = 365;

    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    private readonly ILogger<ReferralService> _log;

    public ReferralService(AppDbContext db, AuditService audit, ILogger<ReferralService> log)
    {
        _db = db;
        _audit = audit;
        _log = log;
    }

    // ─────────────────────────────────────────────────────────────
    // Campañas
    // ─────────────────────────────────────────────────────────────
    public sealed record CampaignDto(Guid Id, string Name, string? Description, DateTime ValidFrom, DateTime ValidTo,
        decimal ReferrerAmount, decimal ReferredAmount, Guid? CategoryId, string? CategoryName, bool IsActive, bool IsCurrent,
        int Referrals, int Earned);

    public sealed class SaveCampaignDto
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public decimal ReferrerAmount { get; set; }
        public decimal ReferredAmount { get; set; }
        public Guid? CategoryId { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public async Task<List<CampaignDto>> CampaignsAsync(Guid academyId)
    {
        var list = await _db.ReferralCampaigns.AsNoTracking().Where(c => c.AcademyId == academyId)
            .OrderByDescending(c => c.ValidFrom).ToListAsync();
        var ids = list.Select(c => c.Id).ToList();
        var counts = await _db.Referrals.AsNoTracking()
            .Where(r => ids.Contains(r.CampaignId) && r.Status != ReferralStatus.Annulled)
            .GroupBy(r => r.CampaignId)
            .Select(g => new { g.Key, Total = g.Count(), Earned = g.Count(r => r.Status != ReferralStatus.Registered) })
            .ToDictionaryAsync(x => x.Key);
        var catIds = list.Where(c => c.CategoryId.HasValue).Select(c => c.CategoryId!.Value).Distinct().ToList();
        var cats = await _db.Categories.AsNoTracking().Where(c => catIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);
        var today = AcademyClock.Today;
        return list.Select(c => new CampaignDto(c.Id, c.Name, c.Description, c.ValidFrom, c.ValidTo, c.ReferrerAmount, c.ReferredAmount,
            c.CategoryId, c.CategoryId.HasValue ? cats.GetValueOrDefault(c.CategoryId.Value) : null, c.IsActive, c.IsValidOn(today),
            counts.TryGetValue(c.Id, out var n) ? n.Total : 0, counts.TryGetValue(c.Id, out var m) ? m.Earned : 0)).ToList();
    }

    public async Task<CampaignDto> SaveCampaignAsync(Guid academyId, Guid? id, SaveCampaignDto dto)
    {
        var name = dto.Name?.Trim() ?? "";
        if (name.Length < 3) throw new Exception("Indica el nombre de la campaña (ej. \"Referidos Diciembre 2026\").");
        if (name.Length > 100) throw new Exception("El nombre de la campaña es muy largo.");
        if (dto.ValidFrom == default || dto.ValidTo == default) throw new Exception("Indica la vigencia de la campaña.");
        var from = dto.ValidFrom.Date;
        var to = dto.ValidTo.Date;
        if (to < from) throw new Exception("La fecha de fin de la vigencia es anterior a la de inicio.");
        if (dto.ReferrerAmount <= 0) throw new Exception("Indica el descuento para quien recomienda (mayor a cero).");
        if (dto.ReferredAmount < 0) throw new Exception("El descuento del alumno nuevo no puede ser negativo.");
        if (dto.ReferrerAmount > 9999 || dto.ReferredAmount > 9999) throw new Exception("El monto del descuento no es válido.");
        if (decimal.Round(dto.ReferrerAmount, 2) != dto.ReferrerAmount || decimal.Round(dto.ReferredAmount, 2) != dto.ReferredAmount)
            throw new Exception("Los montos admiten como máximo dos decimales.");
        if (dto.CategoryId.HasValue && !await _db.Categories.AnyAsync(c => c.Id == dto.CategoryId && c.AcademyId == academyId))
            throw new Exception("La categoría no es válida.");

        // Una sola campaña activa por fecha: así nunca hay duda de qué monto se le prometió a una familia.
        if (dto.IsActive)
        {
            var overlap = await _db.ReferralCampaigns.AsNoTracking()
                .Where(c => c.AcademyId == academyId && c.IsActive && c.Id != id && c.ValidFrom <= to && c.ValidTo >= from)
                .Select(c => c.Name).FirstOrDefaultAsync();
            if (overlap != null) throw new Exception($"Las fechas se cruzan con la campaña activa \"{overlap}\". Ajusta la vigencia o desactívala.");
        }

        ReferralCampaign c;
        if (id.HasValue)
        {
            c = await _db.ReferralCampaigns.FirstOrDefaultAsync(x => x.Id == id && x.AcademyId == academyId)
                ?? throw new KeyNotFoundException("Campaña no encontrada.");
        }
        else
        {
            c = new ReferralCampaign { AcademyId = academyId };
            _db.ReferralCampaigns.Add(c);
        }
        var before = id.HasValue ? new { c.Name, c.ValidFrom, c.ValidTo, c.ReferrerAmount, c.ReferredAmount, c.IsActive } : null;
        c.Name = name;
        c.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim()[..Math.Min(dto.Description.Trim().Length, 300)];
        c.ValidFrom = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        c.ValidTo = DateTime.SpecifyKind(to, DateTimeKind.Utc);
        c.ReferrerAmount = dto.ReferrerAmount;
        c.ReferredAmount = dto.ReferredAmount;
        c.CategoryId = dto.CategoryId;
        c.IsActive = dto.IsActive;
        c.UpdatedAt = DateTime.UtcNow;
        _audit.Add(academyId, id.HasValue ? "referrals.campaign.update" : "referrals.campaign.create", nameof(ReferralCampaign), c.Id,
            new { before, after = new { c.Name, c.ValidFrom, c.ValidTo, c.ReferrerAmount, c.ReferredAmount, c.IsActive } });
        await _db.SaveChangesAsync();
        return (await CampaignsAsync(academyId)).First(x => x.Id == c.Id);
    }

    /// <summary>Una campaña con referidos no se borra (es historia): se desactiva.</summary>
    public async Task DeleteCampaignAsync(Guid academyId, Guid id)
    {
        var c = await _db.ReferralCampaigns.FirstOrDefaultAsync(x => x.Id == id && x.AcademyId == academyId)
            ?? throw new KeyNotFoundException("Campaña no encontrada.");
        if (await _db.Referrals.AnyAsync(r => r.CampaignId == id))
            throw new Exception("La campaña ya tiene referidos registrados: desactívala en lugar de eliminarla.");
        c.IsDeleted = true;
        c.UpdatedAt = DateTime.UtcNow;
        _audit.Add(academyId, "referrals.campaign.delete", nameof(ReferralCampaign), c.Id, new { c.Name });
        await _db.SaveChangesAsync();
    }

    public async Task<CampaignDto?> CurrentCampaignAsync(Guid academyId)
    {
        var all = await CampaignsAsync(academyId);
        return all.FirstOrDefault(c => c.IsCurrent);
    }

    // ─────────────────────────────────────────────────────────────
    // Referidos
    // ─────────────────────────────────────────────────────────────
    public sealed record BenefitDto(Guid Id, string Side, Guid PaymentRecordId, string Debt, decimal Amount, bool DebtPaid, DateTime AppliedAt, DateTime? RevertedAt, string? RevertReason);

    public sealed record ReferralDto(Guid Id, Guid CampaignId, string CampaignName,
        Guid ReferrerStudentId, string ReferrerName, string? ReferrerCategory,
        Guid ReferredStudentId, string ReferredName, string? ReferredCategory,
        decimal ReferrerAmount, decimal ReferrerApplied, decimal ReferrerPending,
        decimal ReferredAmount, decimal ReferredApplied,
        string Status, DateTime CreatedAt, string? CreatedByName, DateTime? EarnedAt, string? Notes,
        string? AnnulReason, DateTime? AnnulledAt, List<BenefitDto> Benefits);

    public sealed class RegisterReferralDto
    {
        public Guid ReferredStudentId { get; set; }
        public Guid ReferrerStudentId { get; set; }
        public string? Notes { get; set; }
    }

    public async Task<ReferralDto> RegisterAsync(Guid academyId, RegisterReferralDto dto, string? actorName)
    {
        if (dto.ReferredStudentId == Guid.Empty || dto.ReferrerStudentId == Guid.Empty)
            throw new Exception("Indica el alumno nuevo y quién lo recomendó.");
        if (dto.ReferredStudentId == dto.ReferrerStudentId) throw new Exception("Un alumno no puede referirse a sí mismo.");

        var students = await _db.Students.AsNoTracking()
            .Where(s => s.AcademyId == academyId && (s.Id == dto.ReferredStudentId || s.Id == dto.ReferrerStudentId))
            .ToDictionaryAsync(s => s.Id);
        if (!students.TryGetValue(dto.ReferredStudentId, out var referred)) throw new KeyNotFoundException("Alumno nuevo no encontrado.");
        if (!students.TryGetValue(dto.ReferrerStudentId, out var referrer)) throw new KeyNotFoundException("Alumno que recomienda no encontrado.");
        if (!referred.IsActive) throw new Exception($"{referred.FirstName} está retirado.");
        if (!referrer.IsActive) throw new Exception($"{referrer.FirstName} {referrer.LastName} está retirado: solo un alumno activo puede recomendar.");
        if (referred.GuardianId.HasValue && referred.GuardianId == referrer.GuardianId)
            throw new Exception("Son hermanos (mismo apoderado): para ellos aplica el descuento de hermanos, no el de referidos.");
        if (!string.IsNullOrWhiteSpace(referred.DocumentNumber) && referred.DocumentNumber == referrer.DocumentNumber)
            throw new Exception("Ambos alumnos tienen el mismo documento.");

        var today = AcademyClock.Today;
        var campaign = await _db.ReferralCampaigns.AsNoTracking()
            .Where(c => c.AcademyId == academyId && c.IsActive && c.ValidFrom <= today && c.ValidTo >= today)
            .FirstOrDefaultAsync()
            ?? throw new Exception("No hay una campaña de referidos vigente hoy. Créala en Finanzas → Descuentos → Referidos.");
        if (campaign.CategoryId.HasValue && campaign.CategoryId != referred.CategoryId)
            throw new Exception($"La campaña \"{campaign.Name}\" es solo para otra categoría.");

        if (await _db.Referrals.AnyAsync(r => r.ReferredStudentId == referred.Id && r.Status != ReferralStatus.Annulled))
            throw new Exception($"{referred.FirstName} ya está registrado como referido.");
        if (await _db.Referrals.AnyAsync(r => r.ReferredStudentId == referrer.Id && r.ReferrerStudentId == referred.Id && r.Status != ReferralStatus.Annulled))
            throw new Exception("No se permiten referidos cruzados entre los mismos alumnos.");

        // Solo cuenta un alumno nuevo: sin pagos, o exalumno que no pagó nada en el último año
        // (también se revisan otras fichas con el mismo documento).
        var since = DateTime.UtcNow.AddDays(-ReturningStudentDays);
        var sameDoc = string.IsNullOrWhiteSpace(referred.DocumentNumber)
            ? new List<Guid>()
            : await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.AcademyId == academyId && s.DocumentNumber == referred.DocumentNumber && s.Id != referred.Id)
                .Select(s => s.Id).ToListAsync();
        var ownPaid = await PaidSinceAsync(new List<Guid> { referred.Id }, null);
        if (ownPaid.HasValue && ownPaid.Value >= since)
            throw new Exception($"{referred.FirstName} ya realizó pagos: el referido se registra antes de su primer cobro.");
        if (sameDoc.Count > 0)
        {
            var otherPaid = await PaidSinceAsync(sameDoc, since);
            if (otherPaid.HasValue)
                throw new Exception("Este alumno ya estuvo en la academia en el último año (otra ficha con el mismo documento): no cuenta como referido.");
        }

        var r = new Referral
        {
            AcademyId = academyId,
            CampaignId = campaign.Id,
            ReferrerStudentId = referrer.Id,
            ReferredStudentId = referred.Id,
            ReferrerAmount = campaign.ReferrerAmount,
            ReferredAmount = campaign.ReferredAmount,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim()[..Math.Min(dto.Notes.Trim().Length, 300)],
            CreatedByName = actorName
        };
        _db.Referrals.Add(r);
        _audit.Add(academyId, "referrals.register", nameof(Referral), r.Id,
            new { referred = referred.Id, referrer = referrer.Id, campaign = campaign.Name, r.ReferrerAmount, r.ReferredAmount });
        await _db.SaveChangesAsync();

        await SyncAsync(academyId, new[] { referred.Id, referrer.Id });
        return (await ListAsync(academyId, null, null, null, r.Id)).First();
    }

    public async Task<ReferralDto> AnnulAsync(Guid academyId, Guid id, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5) throw new Exception("Indica el motivo de la anulación (mínimo 5 caracteres).");
        var r = await _db.Referrals.FirstOrDefaultAsync(x => x.Id == id && x.AcademyId == academyId)
            ?? throw new KeyNotFoundException("Referido no encontrado.");
        if (r.Status == ReferralStatus.Annulled) throw new Exception("El referido ya está anulado.");

        var benefits = await _db.ReferralBenefits.Where(b => b.ReferralId == r.Id && b.RevertedAt == null).ToListAsync();
        var records = await RecordsAsync(benefits.Select(b => b.PaymentRecordId));
        var kept = 0m;
        foreach (var b in benefits)
        {
            records.TryGetValue(b.PaymentRecordId, out var rec);
            if (rec != null && (rec.IsPaid || rec.AmountPaid > 0)) { kept += b.Amount; continue; } // lo ya pagado no se toca
            Revert(b, rec, "Referido anulado");
        }
        r.Status = ReferralStatus.Annulled;
        r.AnnulReason = reason.Trim()[..Math.Min(reason.Trim().Length, 300)];
        r.AnnulledAt = DateTime.UtcNow;
        r.UpdatedAt = DateTime.UtcNow;
        _audit.Add(academyId, "referrals.annul", nameof(Referral), r.Id, new { reason = r.AnnulReason, keptOnPaidDebts = kept });
        await _db.SaveChangesAsync();
        return (await ListAsync(academyId, null, null, null, r.Id)).First();
    }

    public async Task<List<ReferralDto>> ListAsync(Guid academyId, Guid? campaignId, string? status, Guid? studentId, Guid? id = null)
    {
        var q = _db.Referrals.AsNoTracking().IgnoreQueryFilters()
            .Include(r => r.Campaign)
            .Include(r => r.ReferrerStudent).ThenInclude(s => s.Category)
            .Include(r => r.ReferredStudent).ThenInclude(s => s.Category)
            .Where(r => r.AcademyId == academyId);
        if (id.HasValue) q = q.Where(r => r.Id == id);
        if (campaignId.HasValue) q = q.Where(r => r.CampaignId == campaignId);
        if (studentId.HasValue) q = q.Where(r => r.ReferrerStudentId == studentId || r.ReferredStudentId == studentId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReferralStatus>(status, true, out var st)) q = q.Where(r => r.Status == st);
        var list = await q.OrderByDescending(r => r.CreatedAt).Take(1000).ToListAsync();

        var ids = list.Select(r => r.Id).ToList();
        var benefits = await _db.ReferralBenefits.AsNoTracking().Where(b => ids.Contains(b.ReferralId)).OrderBy(b => b.AppliedAt).ToListAsync();
        var recIds = benefits.Select(b => b.PaymentRecordId).Distinct().ToList();
        var recs = await _db.PaymentRecords.AsNoTracking().IgnoreQueryFilters().Where(p => recIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.Description, p.IsPaid });
        var byReferral = benefits.ToLookup(b => b.ReferralId);

        return list.Select(r =>
        {
            var mine = byReferral[r.Id].ToList();
            var referrerApplied = mine.Where(b => b.Side == ReferralSide.Referrer && b.RevertedAt == null).Sum(b => b.Amount);
            var referredApplied = mine.Where(b => b.Side == ReferralSide.Referred && b.RevertedAt == null).Sum(b => b.Amount);
            return new ReferralDto(r.Id, r.CampaignId, r.Campaign.Name,
                r.ReferrerStudentId, Name(r.ReferrerStudent), r.ReferrerStudent.Category?.Name,
                r.ReferredStudentId, Name(r.ReferredStudent), r.ReferredStudent.Category?.Name,
                r.ReferrerAmount, referrerApplied, r.Status == ReferralStatus.Annulled ? 0 : Math.Max(0, r.ReferrerAmount - referrerApplied),
                r.ReferredAmount, referredApplied,
                r.Status.ToString(), r.CreatedAt, r.CreatedByName, r.EarnedAt, r.Notes, r.AnnulReason, r.AnnulledAt,
                mine.Select(b => new BenefitDto(b.Id, b.Side.ToString(), b.PaymentRecordId,
                    recs.TryGetValue(b.PaymentRecordId, out var p) ? p.Description : "(deuda eliminada)", b.Amount,
                    p?.IsPaid ?? false, b.AppliedAt, b.RevertedAt, b.RevertReason)).ToList());
        }).ToList();
    }

    public sealed record TopReferrerDto(Guid StudentId, string Name, int Referrals, int Earned, decimal Benefit);
    public sealed record SummaryDto(int Registered, int Earned, int Completed, int Annulled, decimal Conversion,
        decimal ReferrerCost, decimal ReferredCost, decimal PendingToApply, List<TopReferrerDto> TopReferrers);

    /// <summary>Indicadores del programa (por campaña o de todas).</summary>
    public async Task<SummaryDto> SummaryAsync(Guid academyId, Guid? campaignId)
    {
        var list = await ListAsync(academyId, campaignId, null, null);
        var valid = list.Where(r => r.Status != nameof(ReferralStatus.Annulled)).ToList();
        var earned = valid.Count(r => r.Status != nameof(ReferralStatus.Registered));
        var top = valid.GroupBy(r => new { r.ReferrerStudentId, r.ReferrerName })
            .Select(g => new TopReferrerDto(g.Key.ReferrerStudentId, g.Key.ReferrerName, g.Count(),
                g.Count(r => r.Status != nameof(ReferralStatus.Registered)), g.Sum(r => r.ReferrerApplied)))
            .OrderByDescending(t => t.Earned).ThenByDescending(t => t.Referrals).Take(10).ToList();
        return new SummaryDto(
            valid.Count(r => r.Status == nameof(ReferralStatus.Registered)), earned,
            valid.Count(r => r.Status == nameof(ReferralStatus.Completed)),
            list.Count(r => r.Status == nameof(ReferralStatus.Annulled)),
            valid.Count == 0 ? 0 : Math.Round(100m * earned / valid.Count, 1),
            valid.Sum(r => r.ReferrerApplied), valid.Sum(r => r.ReferredApplied),
            valid.Where(r => r.Status != nameof(ReferralStatus.Registered)).Sum(r => r.ReferrerPending), top);
    }

    // ─────────────────────────────────────────────────────────────
    // Aplicación de beneficios
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Recalcula los referidos de estos alumnos (o de toda la academia) y aplica o revierte sus beneficios.
    /// Idempotente: se puede llamar después de cualquier cobro, anulación o generación de deudas.
    /// </summary>
    public async Task SyncAsync(Guid academyId, IEnumerable<Guid>? studentIds = null, CancellationToken ct = default)
    {
        var ids = studentIds?.Distinct().ToList();
        if (ids is { Count: 0 }) return;
        var q = _db.Referrals.Include(r => r.ReferredStudent).Include(r => r.ReferrerStudent)
            .Where(r => r.AcademyId == academyId && r.Status != ReferralStatus.Annulled);
        if (ids != null) q = q.Where(r => ids.Contains(r.ReferredStudentId) || ids.Contains(r.ReferrerStudentId));
        var referrals = await q.OrderBy(r => r.CreatedAt).ToListAsync(ct);
        if (referrals.Count == 0) return;

        foreach (var r in referrals) await SyncOneAsync(r, ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Igual que <see cref="SyncAsync"/>, pero un fallo nunca interrumpe la operación que lo llamó (cobro, anulación…).</summary>
    public async Task TrySyncAsync(Guid academyId, IEnumerable<Guid>? studentIds)
    {
        try
        {
            await SyncAsync(academyId, studentIds);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudieron recalcular los referidos de la academia {Academy}", academyId);
            _db.ChangeTracker.Clear();
        }
    }

    /// <summary>Todas las academias con referidos pendientes (tarea diaria de respaldo).</summary>
    public async Task<int> SyncAllAsync(CancellationToken ct = default)
    {
        var academies = await _db.Referrals.AsNoTracking()
            .Where(r => r.Status == ReferralStatus.Registered || r.Status == ReferralStatus.Earned)
            .Select(r => r.AcademyId).Distinct().ToListAsync(ct);
        foreach (var a in academies)
        {
            await TrySyncAsync(a, null);
            _db.ChangeTracker.Clear();
        }
        return academies.Count;
    }

    private async Task SyncOneAsync(Referral r, CancellationToken ct)
    {
        var benefits = await _db.ReferralBenefits.Where(b => b.ReferralId == r.Id).ToListAsync(ct);
        var active = benefits.Where(b => b.RevertedAt == null).ToList();
        var records = await RecordsAsync(active.Select(b => b.PaymentRecordId));

        // 1) Descuentos sobre deudas que ya no existen (regeneradas o eliminadas): vuelven a estar disponibles.
        foreach (var b in active.ToList())
        {
            if (!records.TryGetValue(b.PaymentRecordId, out var rec) || rec.IsDeleted)
            {
                Revert(b, null, "La deuda se eliminó o regeneró");
                active.Remove(b);
            }
        }

        // 2) ¿Pagó el referido su primer cobro? (pagos posteriores al registro; un pago anulado deja de contar)
        var firstPaid = await PaidSinceAsync(new List<Guid> { r.ReferredStudentId }, r.CreatedAt, firstOnly: true);
        var earned = firstPaid.HasValue;

        // 3) Bienvenida del referido: en su primera deuda, mientras no haya pagado nada.
        if (r.ReferredAmount > 0 && !earned && !active.Any(b => b.Side == ReferralSide.Referred))
        {
            var debt = await _db.PaymentRecords
                .Where(p => p.AcademyId == r.AcademyId && p.StudentId == r.ReferredStudentId && !p.IsPaid && p.AmountPaid == 0
                            && p.Amount > 0 && p.ExclusionType == ExclusionType.None
                            && p.Type != PaymentType.LateFee && p.Type != PaymentType.ProductSale)
                .OrderBy(p => p.DueDate).ThenBy(p => p.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (debt != null)
                active.Add(Apply(r, ReferralSide.Referred, debt, Math.Min(r.ReferredAmount, debt.Amount), $"Bienvenida referido S/ {Money(Math.Min(r.ReferredAmount, debt.Amount))}"));
        }

        if (earned)
        {
            r.EarnedAt ??= firstPaid!.Value;

            // 4) Beneficio del referente: en su siguiente mensualidad; el saldo pasa a la siguiente.
            var pending = r.ReferrerAmount - active.Where(b => b.Side == ReferralSide.Referrer).Sum(b => b.Amount);
            if (pending > 0.009m && r.ReferrerStudent.IsActive && !r.ReferrerStudent.IsDeleted)
            {
                var debts = await _db.PaymentRecords
                    .Where(p => p.AcademyId == r.AcademyId && p.StudentId == r.ReferrerStudentId && !p.IsPaid
                                && p.Amount - p.AmountPaid > 0.009m && p.ExclusionType == ExclusionType.None
                                && (p.Type == PaymentType.MonthlyFee || (p.Type == PaymentType.Sessions && p.SessionDate == null)))
                    .OrderBy(p => p.DueDate).ThenBy(p => p.CreatedAt)
                    .ToListAsync(ct);
                foreach (var debt in debts)
                {
                    if (pending <= 0.009m) break;
                    var amount = Math.Min(pending, debt.Amount - debt.AmountPaid);
                    var name = $"{r.ReferredStudent.FirstName} {r.ReferredStudent.LastName}".Trim();
                    var b = Apply(r, ReferralSide.Referrer, debt, amount, Truncate($"Referido {name} S/ {Money(amount)}", 150));
                    active.Add(b);
                    pending -= amount;
                    if (r.NotifiedAt == null) Notify(r, debt, amount);
                }
            }
        }
        else if (r.EarnedAt != null)
        {
            // El pago que lo hizo ganar se anuló: se retira el beneficio del referente donde aún no se pagó.
            r.EarnedAt = null;
            var mine = active.Where(b => b.Side == ReferralSide.Referrer).ToList();
            var recs = await RecordsAsync(mine.Select(b => b.PaymentRecordId));
            foreach (var b in mine)
            {
                recs.TryGetValue(b.PaymentRecordId, out var rec);
                if (rec != null && (rec.IsPaid || rec.AmountPaid > 0)) continue;
                Revert(b, rec, "Se anuló el primer pago del referido");
                active.Remove(b);
            }
        }

        var referrerApplied = active.Where(b => b.Side == ReferralSide.Referrer).Sum(b => b.Amount);
        var status = !earned && r.EarnedAt == null ? ReferralStatus.Registered
            : referrerApplied >= r.ReferrerAmount - 0.009m ? ReferralStatus.Completed
            : ReferralStatus.Earned;
        if (status != r.Status)
        {
            _audit.Add(r.AcademyId, "referrals.status", nameof(Referral), r.Id, new { from = r.Status.ToString(), to = status.ToString() });
            r.Status = status;
            r.UpdatedAt = DateTime.UtcNow;
        }
    }

    private ReferralBenefit Apply(Referral r, ReferralSide side, PaymentRecord debt, decimal amount, string label)
    {
        amount = Math.Round(amount, 2);
        debt.Amount -= amount;
        debt.DiscountAmount += amount;
        debt.ReferralDiscount += amount;
        debt.DiscountDetail = Truncate(Join(debt.DiscountDetail, label), 200);
        if (debt.AmountPaid >= debt.Amount - 0.01m)
        {
            debt.AmountPaid = Math.Max(0, Math.Min(debt.AmountPaid, debt.Amount));
            debt.IsPaid = true;
            debt.PaidDate ??= DateTime.UtcNow;
        }
        debt.UpdatedAt = DateTime.UtcNow;
        var b = new ReferralBenefit
        {
            AcademyId = r.AcademyId, ReferralId = r.Id, Side = side, PaymentRecordId = debt.Id,
            StudentId = debt.StudentId, Amount = amount, Label = label
        };
        _db.ReferralBenefits.Add(b);
        _audit.Add(r.AcademyId, "referrals.benefit.apply", nameof(PaymentRecord), debt.Id, new { referral = r.Id, side = side.ToString(), amount });
        return b;
    }

    private void Revert(ReferralBenefit b, PaymentRecord? debt, string reason)
    {
        b.RevertedAt = DateTime.UtcNow;
        b.RevertReason = reason;
        if (debt != null && !debt.IsDeleted)
        {
            debt.Amount += b.Amount;
            debt.DiscountAmount = Math.Max(0, debt.DiscountAmount - b.Amount);
            debt.ReferralDiscount = Math.Max(0, debt.ReferralDiscount - b.Amount);
            debt.DiscountDetail = RemoveLabel(debt.DiscountDetail, b.Label);
            if (debt.AmountPaid < debt.Amount - 0.01m)
            {
                debt.IsPaid = false;
                debt.PaidDate = null;
            }
            debt.UpdatedAt = DateTime.UtcNow;
        }
        _audit.Add(b.AcademyId, "referrals.benefit.revert", nameof(PaymentRecord), b.PaymentRecordId, new { referral = b.ReferralId, b.Amount, reason });
    }

    /// <summary>
    /// Tras recalcular el monto de una deuda (generación con abonos, liquidación, ajuste), vuelve a descontar
    /// los beneficios de referidos que tenía. Si la deuda bajó y ya no alcanza, el sobrante se libera y se
    /// aplicará en la siguiente deuda en la próxima sincronización. No guarda.
    /// </summary>
    public async Task ReapplyAsync(PaymentRecord record)
    {
        if (record.ReferralDiscount <= 0) return;
        var benefits = await _db.ReferralBenefits
            .Where(b => b.PaymentRecordId == record.Id && b.RevertedAt == null).OrderBy(b => b.AppliedAt).ToListAsync();
        record.ReferralDiscount = 0;
        foreach (var b in benefits)
        {
            var room = Math.Max(0, record.Amount - record.AmountPaid);
            if (room <= 0.009m)
            {
                b.RevertedAt = DateTime.UtcNow;
                b.RevertReason = "La deuda se recalculó";
                continue;
            }
            if (b.Amount > room)
            {
                // Se parte: lo que cabe queda aquí; el resto se libera para la siguiente deuda.
                b.RevertedAt = DateTime.UtcNow;
                b.RevertReason = "La deuda se recalculó";
                var label = b.Label.Contains("S/ ") ? b.Label[..b.Label.LastIndexOf("S/ ", StringComparison.Ordinal)] + $"S/ {Money(room)}" : b.Label;
                var part = new ReferralBenefit
                {
                    AcademyId = b.AcademyId, ReferralId = b.ReferralId, Side = b.Side, PaymentRecordId = record.Id,
                    StudentId = record.StudentId, Amount = Math.Round(room, 2), Label = label
                };
                _db.ReferralBenefits.Add(part);
                ApplyAmount(record, part.Amount, part.Label);
                continue;
            }
            ApplyAmount(record, b.Amount, b.Label);
        }
    }

    private static void ApplyAmount(PaymentRecord record, decimal amount, string label)
    {
        record.Amount -= amount;
        record.DiscountAmount += amount;
        record.ReferralDiscount += amount;
        if (record.DiscountDetail == null || !record.DiscountDetail.Contains(label))
            record.DiscountDetail = Truncate(Join(record.DiscountDetail, label), 200);
        if (record.AmountPaid >= record.Amount - 0.01m)
        {
            record.IsPaid = true;
            record.PaidDate ??= DateTime.UtcNow;
        }
    }

    private void Notify(Referral r, PaymentRecord debt, decimal amount)
    {
        r.NotifiedAt = DateTime.UtcNow;
        if (r.ReferrerStudent.GuardianId == null && r.ReferrerStudent.UserId == null) return;
        _db.FamilyNotices.Add(new FamilyNotice
        {
            AcademyId = r.AcademyId,
            StudentId = r.ReferrerStudentId,
            PaymentRecordId = debt.Id,
            DedupeKey = $"ref:{r.Id}",
            Type = FamilyNoticeType.PaymentReminder,
            Title = "¡Gracias por recomendarnos!",
            Body = $"{r.ReferredStudent.FirstName} ya es parte de la academia. Tienes S/ {Money(r.ReferrerAmount)} de descuento" +
                   (amount >= r.ReferrerAmount - 0.009m ? $" en {debt.Description}." : $": S/ {Money(amount)} en {debt.Description} y el resto en tu siguiente cobro.")
        });
    }

    /// <summary>Beneficios de referidos por aplicar a estos alumnos (para mostrarlos en caja).</summary>
    public async Task<Dictionary<Guid, decimal>> PendingCreditAsync(Guid academyId, IEnumerable<Guid> studentIds)
    {
        var ids = studentIds.Distinct().ToList();
        var refs = await _db.Referrals.AsNoTracking()
            .Where(r => r.AcademyId == academyId && ids.Contains(r.ReferrerStudentId) && r.Status == ReferralStatus.Earned)
            .Select(r => new { r.Id, r.ReferrerStudentId, r.ReferrerAmount }).ToListAsync();
        if (refs.Count == 0) return new();
        var refIds = refs.Select(r => r.Id).ToList();
        var applied = await _db.ReferralBenefits.AsNoTracking()
            .Where(b => refIds.Contains(b.ReferralId) && b.RevertedAt == null && b.Side == ReferralSide.Referrer)
            .GroupBy(b => b.ReferralId).Select(g => new { g.Key, Sum = g.Sum(b => b.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Sum);
        return refs.GroupBy(r => r.ReferrerStudentId)
            .ToDictionary(g => g.Key, g => g.Sum(r => Math.Max(0, r.ReferrerAmount - applied.GetValueOrDefault(r.Id))));
    }

    // ── Utilidades ──
    /// <summary>Fecha del primer (o último) pago de estos alumnos desde una fecha; null si no hay.</summary>
    private async Task<DateTime?> PaidSinceAsync(List<Guid> studentIds, DateTime? since, bool firstOnly = false)
    {
        var q = _db.PaymentInstallments.AsNoTracking()
            .Where(i => i.AmountPaid > 0 && studentIds.Contains(i.PaymentRecord.StudentId) && !i.PaymentRecord.IsDeleted);
        if (since.HasValue) { var from = since.Value; q = q.Where(i => i.PaidAt >= from); }
        return firstOnly
            ? await q.OrderBy(i => i.PaidAt).Select(i => (DateTime?)i.PaidAt).FirstOrDefaultAsync()
            : await q.OrderByDescending(i => i.PaidAt).Select(i => (DateTime?)i.PaidAt).FirstOrDefaultAsync();
    }

    private async Task<Dictionary<Guid, PaymentRecord>> RecordsAsync(IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return new();
        return await _db.PaymentRecords.IgnoreQueryFilters().Where(p => list.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
    }

    private static string Name(Student s) => $"{s.FirstName} {s.LastName}".Trim();
    private static string Money(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Join(string? a, string b) => string.IsNullOrWhiteSpace(a) ? b : $"{a} · {b}";
    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private static string? RemoveLabel(string? detail, string label)
    {
        if (string.IsNullOrEmpty(detail)) return detail;
        var parts = detail.Split(" · ").ToList();
        var i = parts.IndexOf(label);
        if (i >= 0) parts.RemoveAt(i);
        var result = string.Join(" · ", parts).Trim();
        return result.Length == 0 ? null : result;
    }
}
