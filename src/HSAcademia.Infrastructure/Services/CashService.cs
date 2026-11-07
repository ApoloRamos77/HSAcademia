using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Finances;
using HSAcademia.Domain.Entities;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Cuadre de caja por cobrador:
/// 1) Cada recibo queda a nombre de quien lo cobró.
/// 2) El cobrador "rinde caja": declara el efectivo que entrega; sus recibos pendientes quedan en esa rendición.
/// 3) Finanzas recibe y cuenta el efectivo; se registran las diferencias (sobrante / faltante).
/// Los pagos digitales (Yape, Plin, transferencia, tarjeta) se informan para conciliar con el banco.
/// </summary>
public class CashService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public CashService(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private static List<CashMethodTotalDto> ByMethod(IEnumerable<Receipt> receipts) =>
        receipts.GroupBy(r => r.Method)
            .Select(g => new CashMethodTotalDto { Method = g.Key.ToString(), Count = g.Count(), Total = g.Sum(r => r.Total) })
            .OrderByDescending(m => m.Total)
            .ToList();

    private async Task<string> UserNameAsync(Guid userId) =>
        await _db.Users.Where(u => u.Id == userId).Select(u => (u.FirstName + " " + u.LastName).Trim()).FirstOrDefaultAsync() ?? "Usuario";

    private static DateTime LocalDayStartUtc(DateTime localDate) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified), AcademyClock.Zone);

    // ── Mi caja (cobrador) ────────────────────────────────────────
    public async Task<MyCashDto> MyCashAsync(Guid academyId, Guid userId)
    {
        var pending = await _db.Receipts.AsNoTracking().Include(r => r.Lines)
            .Where(r => r.AcademyId == academyId && r.CreatedByUserId == userId &&
                        r.Status == ReceiptStatus.Issued && r.CashHandoverId == null)
            .OrderByDescending(r => r.IssuedAt)
            .ToListAsync();
        var recent = await _db.CashHandovers.AsNoTracking()
            .Where(h => h.AcademyId == academyId && h.CollectorUserId == userId)
            .OrderByDescending(h => h.DeclaredAt).Take(15).ToListAsync();

        return new MyCashDto
        {
            CollectorName = await UserNameAsync(userId),
            PendingReceipts = pending.Count,
            PendingCash = pending.Where(r => r.Method == PaymentMethod.Cash).Sum(r => r.Total),
            PendingDigital = pending.Where(r => r.Method != PaymentMethod.Cash).Sum(r => r.Total),
            ByMethod = ByMethod(pending),
            Receipts = pending.Select(ReceiptService.Map).ToList(),
            RecentHandovers = recent.Select(Map).ToList()
        };
    }

    /// <summary>El cobrador rinde caja: todos sus recibos aún no rendidos pasan a esta rendición.</summary>
    public async Task<CashHandoverDto> DeclareAsync(Guid academyId, Guid userId, DeclareHandoverDto dto)
    {
        if (dto.DeclaredCash < 0) throw new Exception("El efectivo declarado no puede ser negativo.");

        return await _db.InTransactionAsync(async () =>
        {
            var pending = await _db.Receipts.AsNoTracking()
                .Where(r => r.AcademyId == academyId && r.CreatedByUserId == userId &&
                            r.Status == ReceiptStatus.Issued && r.CashHandoverId == null)
                .Select(r => new { r.Id, r.Method, r.Total })
                .ToListAsync();
            if (pending.Count == 0) throw new Exception("No tienes cobros pendientes de rendir.");

            var expected = pending.Where(r => r.Method == PaymentMethod.Cash).Sum(r => r.Total);
            var digital = pending.Where(r => r.Method != PaymentMethod.Cash).Sum(r => r.Total);
            var diff = dto.DeclaredCash - expected;
            if (Math.Abs(diff) >= 0.01m && string.IsNullOrWhiteSpace(dto.Notes))
                throw new Exception($"El efectivo declarado no coincide con lo cobrado (S/ {expected:N2}). Explica la diferencia en la nota.");

            var handover = new CashHandover
            {
                AcademyId = academyId,
                CollectorUserId = userId,
                CollectorName = await UserNameAsync(userId),
                DeclaredAt = DateTime.UtcNow,
                ReceiptsCount = pending.Count,
                ExpectedCash = expected,
                DigitalTotal = digital,
                DeclaredCash = dto.DeclaredCash,
                Notes = ReceiptRecorder.Truncate(dto.Notes?.Trim(), 500),
                Status = CashHandoverStatus.Pending
            };
            _db.CashHandovers.Add(handover);
            _audit.Add(academyId, "finances.cash.declare", nameof(CashHandover), handover.Id,
                new { expected, digital, dto.DeclaredCash, receipts = pending.Count });
            await _db.SaveChangesAsync();

            // Asignación condicionada: si otro envío simultáneo ya tomó algún recibo, se revierte todo.
            var ids = pending.Select(r => r.Id).ToList();
            var assigned = await _db.Receipts
                .Where(r => ids.Contains(r.Id) && r.CashHandoverId == null && r.Status == ReceiptStatus.Issued)
                .ExecuteUpdateAsync(set => set.SetProperty(r => r.CashHandoverId, handover.Id));
            if (assigned != ids.Count)
                throw new Exception("Tus recibos cambiaron mientras rendías caja (otra rendición o una anulación). Actualiza e inténtalo nuevamente.");

            return Map(handover);
        });
    }

    // ── Finanzas ──────────────────────────────────────────────────
    public async Task<CashHandoverDto> ReceiveAsync(Guid academyId, Guid receiverId, bool isAdmin, Guid handoverId, ReceiveHandoverDto dto)
    {
        if (dto.ReceivedCash < 0) throw new Exception("El efectivo recibido no puede ser negativo.");
        var h = await _db.CashHandovers.FirstOrDefaultAsync(x => x.Id == handoverId && x.AcademyId == academyId)
            ?? throw new KeyNotFoundException("Rendición no encontrada.");
        if (h.Status == CashHandoverStatus.Received) throw new Exception("Esta rendición ya fue recibida.");
        if (h.CollectorUserId == receiverId && !isAdmin)
            throw new Exception("Otra persona de finanzas debe recibir tu rendición de caja.");
        var diff = dto.ReceivedCash - h.ExpectedCash;
        if (Math.Abs(diff) >= 0.01m && string.IsNullOrWhiteSpace(dto.Notes))
            throw new Exception($"Lo recibido no coincide con lo esperado (S/ {h.ExpectedCash:N2}). Registra el motivo de la diferencia.");

        h.Status = CashHandoverStatus.Received;
        h.ReceivedByUserId = receiverId;
        h.ReceivedByName = await UserNameAsync(receiverId);
        h.ReceivedAt = DateTime.UtcNow;
        h.ReceivedCash = dto.ReceivedCash;
        h.ReceivedNotes = ReceiptRecorder.Truncate(dto.Notes?.Trim(), 500);
        _audit.Add(academyId, "finances.cash.receive", nameof(CashHandover), h.Id,
            new { h.ExpectedCash, h.DeclaredCash, dto.ReceivedCash, difference = diff, collector = h.CollectorName });
        await _db.SaveChangesAsync();
        return Map(h);
    }

    /// <summary>Cuadre del día: lo cobrado por cada cobrador y por medio de pago, y lo pendiente de rendir.</summary>
    public async Task<CashDaySummaryDto> DaySummaryAsync(Guid academyId, DateTime localDate)
    {
        var start = LocalDayStartUtc(localDate);
        var end = LocalDayStartUtc(localDate.Date.AddDays(1));

        var day = await _db.Receipts.AsNoTracking()
            .Where(r => r.AcademyId == academyId && r.IssuedAt >= start && r.IssuedAt < end)
            .ToListAsync();
        var issued = day.Where(r => r.Status == ReceiptStatus.Issued).ToList();

        // Efectivo aún no rendido (de cualquier día) y rendiciones en camino.
        var notHanded = await _db.Receipts.AsNoTracking()
            .Where(r => r.AcademyId == academyId && r.Status == ReceiptStatus.Issued && r.CashHandoverId == null &&
                        r.Channel != ReceiptRecorder.HistoricChannel)
            .Select(r => new { r.CreatedByUserId, r.CreatedByName, r.Method, r.Total })
            .ToListAsync();
        var inTransit = await _db.CashHandovers.AsNoTracking()
            .Where(h => h.AcademyId == academyId && h.Status == CashHandoverStatus.Pending)
            .ToListAsync();
        var handoversOfDay = await _db.CashHandovers.AsNoTracking()
            .Where(h => h.AcademyId == academyId &&
                        ((h.DeclaredAt >= start && h.DeclaredAt < end) || h.Status == CashHandoverStatus.Pending))
            .OrderByDescending(h => h.DeclaredAt)
            .ToListAsync();

        var collectorIds = issued.Select(r => r.CreatedByUserId)
            .Concat(notHanded.Select(r => r.CreatedByUserId))
            .Concat(inTransit.Select(h => (Guid?)h.CollectorUserId))
            .Distinct().ToList();

        var collectors = collectorIds.Select(id =>
        {
            var mine = issued.Where(r => r.CreatedByUserId == id).ToList();
            var pend = notHanded.Where(r => r.CreatedByUserId == id).ToList();
            var name = mine.Select(r => r.CreatedByName).FirstOrDefault(n => n != null)
                       ?? pend.Select(r => r.CreatedByName).FirstOrDefault(n => n != null)
                       ?? inTransit.Where(h => h.CollectorUserId == id).Select(h => h.CollectorName).FirstOrDefault()
                       ?? (id == null ? "Registros anteriores (sin cobrador)" : "Sin usuario");
            return new CashCollectorSummaryDto
            {
                CollectorUserId = id,
                CollectorName = name,
                ReceiptsCount = mine.Count,
                Total = mine.Sum(r => r.Total),
                Cash = mine.Where(r => r.Method == PaymentMethod.Cash).Sum(r => r.Total),
                Digital = mine.Where(r => r.Method != PaymentMethod.Cash).Sum(r => r.Total),
                ByMethod = ByMethod(mine),
                PendingCash = pend.Where(r => r.Method == PaymentMethod.Cash).Sum(r => r.Total),
                PendingReceipts = pend.Count,
                InTransitCash = inTransit.Where(h => h.CollectorUserId == id).Sum(h => h.DeclaredCash)
            };
        })
        .Where(c => c.ReceiptsCount > 0 || c.PendingReceipts > 0 || c.InTransitCash > 0)
        .OrderByDescending(c => c.Total).ToList();

        return new CashDaySummaryDto
        {
            Date = localDate.Date,
            Total = issued.Sum(r => r.Total),
            Cash = issued.Where(r => r.Method == PaymentMethod.Cash).Sum(r => r.Total),
            Digital = issued.Where(r => r.Method != PaymentMethod.Cash).Sum(r => r.Total),
            ReceiptsCount = issued.Count,
            VoidedCount = day.Count - issued.Count,
            ByMethod = ByMethod(issued),
            Collectors = collectors,
            Handovers = handoversOfDay.Select(Map).ToList()
        };
    }

    public async Task<List<CashHandoverDto>> ListAsync(Guid academyId, DateTime? from, DateTime? to, string? status)
    {
        var q = _db.CashHandovers.AsNoTracking().Where(h => h.AcademyId == academyId);
        if (from.HasValue) { var s = LocalDayStartUtc(from.Value); q = q.Where(h => h.DeclaredAt >= s); }
        if (to.HasValue) { var e = LocalDayStartUtc(to.Value.AddDays(1)); q = q.Where(h => h.DeclaredAt < e); }
        if (Enum.TryParse<CashHandoverStatus>(status, true, out var st)) q = q.Where(h => h.Status == st);
        return (await q.OrderByDescending(h => h.DeclaredAt).Take(200).ToListAsync()).Select(Map).ToList();
    }

    public async Task<List<ReceiptDto>> HandoverReceiptsAsync(Guid academyId, Guid handoverId)
    {
        var list = await _db.Receipts.AsNoTracking().Include(r => r.Lines)
            .Where(r => r.AcademyId == academyId && r.CashHandoverId == handoverId)
            .OrderBy(r => r.IssuedAt).ToListAsync();
        return list.Select(ReceiptService.Map).ToList();
    }

    /// <summary>
    /// Al anular un recibo que está en una rendición aún no recibida, se recalculan sus totales
    /// (el recibo sale de la rendición). Si ya fue recibida, se conserva tal como se contó.
    /// </summary>
    internal static async Task DetachVoidedAsync(AppDbContext db, Receipt receipt)
    {
        if (!receipt.CashHandoverId.HasValue) return;
        var h = await db.CashHandovers.FirstOrDefaultAsync(x => x.Id == receipt.CashHandoverId);
        if (h == null || h.Status != CashHandoverStatus.Pending) return;
        h.ReceiptsCount = Math.Max(0, h.ReceiptsCount - 1);
        if (receipt.Method == PaymentMethod.Cash) h.ExpectedCash -= receipt.Total;
        else h.DigitalTotal -= receipt.Total;
        receipt.CashHandoverId = null;
    }

    internal static CashHandoverDto Map(CashHandover h) => new()
    {
        Id = h.Id,
        CollectorUserId = h.CollectorUserId,
        CollectorName = h.CollectorName,
        DeclaredAt = h.DeclaredAt,
        ReceiptsCount = h.ReceiptsCount,
        ExpectedCash = h.ExpectedCash,
        DigitalTotal = h.DigitalTotal,
        DeclaredCash = h.DeclaredCash,
        DeclaredDifference = h.DeclaredCash - h.ExpectedCash,
        Notes = h.Notes,
        Status = h.Status.ToString(),
        ReceivedByName = h.ReceivedByName,
        ReceivedAt = h.ReceivedAt,
        ReceivedCash = h.ReceivedCash,
        ReceivedDifference = h.ReceivedCash.HasValue ? h.ReceivedCash - h.ExpectedCash : null,
        ReceivedNotes = h.ReceivedNotes
    };
}
