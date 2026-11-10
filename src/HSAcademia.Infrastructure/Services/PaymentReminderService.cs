using System.Globalization;
using HSAcademia.Domain.Entities;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Recordatorios de pago de mensualidades, enviados por el servidor para que lleguen aunque la familia no abra la app.
/// Política: se paga del 1 al día de vencimiento (ej. 5). Avisos a partir de las 9:00, hora de Lima:
/// 4 días antes (inicio del periodo de pago), un día antes, el día del vencimiento, y si sigue sin pagarse,
/// al día siguiente y luego a los 5 y 9 días, avisando que puede aplicarse mora.
/// Cada aviso se crea una sola vez; el envío lo hace el despachador de notificaciones.
/// </summary>
public class PaymentReminderService
{
    /// <summary>Días respecto del vencimiento en que se avisa (negativo: antes).</summary>
    private static readonly int[] Offsets = { -4, -1, 0, 1, 5, 9 };
    private const int FirstHour = 9;
    /// <summary>Después de esta hora no se crean avisos: el del día se pierde antes que molestar de noche.</summary>
    private const int LastHour = 21;

    private static readonly string[] Months =
        { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };

    private readonly AppDbContext _db;

    public PaymentReminderService(AppDbContext db) => _db = db;

    /// <summary>Crea los recordatorios que tocan a esta hora. Devuelve cuántos creó.</summary>
    public async Task<int> CreateDueRemindersAsync(DateTime localNow, CancellationToken cancellationToken = default)
    {
        if (localNow.Hour < FirstHour || localNow.Hour >= LastHour) return 0;

        var today = localNow.Date;
        var from = DateTime.SpecifyKind(today.AddDays(-Offsets.Max()), DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(today.AddDays(-Offsets.Min() + 1), DateTimeKind.Utc);

        var fees = await _db.PaymentRecords.AsNoTracking()
            .Where(p => p.Type == PaymentType.MonthlyFee && !p.IsPaid && !p.IsDeleted &&
                        p.ExclusionType == ExclusionType.None && p.Amount - p.AmountPaid > 0 &&
                        p.DueDate >= from && p.DueDate < to &&
                        p.Student.IsActive && (p.Student.GuardianId != null || p.Student.UserId != null))
            // Con un comprobante por revisar la familia ya pagó: no se le recuerda.
            .Where(p => !_db.PaymentProofs.Any(x => x.PaymentRecordId == p.Id && x.Status == PaymentProofStatus.Pending))
            .Select(p => new
            {
                p.Id, p.AcademyId, p.StudentId, p.Description, p.DueDate, p.RegularizedAt,
                Pending = p.Amount - p.AmountPaid, p.Student.FirstName
            })
            .ToListAsync(cancellationToken);

        var candidates = fees
            .Select(f => new { Fee = f, Offset = (today - f.DueDate.Date).Days })
            .Where(c => Offsets.Contains(c.Offset))
            // Ya regularizada (con o sin mora): administración la atendió, no se insiste.
            .Where(c => c.Offset <= 0 || c.Fee.RegularizedAt == null)
            .Select(c => new { c.Fee, c.Offset, Key = $"pay:{c.Fee.Id}:{c.Offset}" })
            .ToList();
        if (candidates.Count == 0) return 0;

        var keys = candidates.Select(c => c.Key).ToList();
        var existing = (await _db.FamilyNotices.AsNoTracking()
            .Where(n => n.DedupeKey != null && keys.Contains(n.DedupeKey))
            .Select(n => n.DedupeKey!)
            .ToListAsync(cancellationToken)).ToHashSet();

        var created = 0;
        foreach (var c in candidates.Where(c => !existing.Contains(c.Key)))
        {
            var (title, body) = Text(c.Offset, $"{c.Fee.Description} de {c.Fee.FirstName}", c.Fee.Pending, c.Fee.DueDate);
            _db.FamilyNotices.Add(new FamilyNotice
            {
                AcademyId = c.Fee.AcademyId,
                StudentId = c.Fee.StudentId,
                PaymentRecordId = c.Fee.Id,
                DedupeKey = c.Key,
                Type = FamilyNoticeType.PaymentReminder,
                Title = title,
                Body = body
            });
            created++;
        }

        try { await _db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return 0; /* otra instancia los creó a la vez: el índice único lo impidió */ }
        return created;
    }

    public static (string Title, string Body) Text(int offset, string what, decimal pending, DateTime dueDate)
    {
        var amount = $"S/ {pending.ToString("0.00", CultureInfo.InvariantCulture)}";
        var due = $"{dueDate.Day} de {Months[dueDate.Month - 1]}";
        return offset switch
        {
            -4 => ("Ya puedes pagar tu mensualidad", $"{what}: {amount}. Tienes hasta el {due}."),
            -1 => ("Tu mensualidad vence mañana", $"{what}: {amount}. Paga por Yape, Plin o transferencia y envía tu comprobante en la app."),
            0 => ("Hoy vence tu mensualidad", $"{what}: {amount}. Evita la mora pagando hoy."),
            // Mismos textos que usaba la app hasta la 1.0.11.
            1 => ("Tu pago venció", $"{what} venció el {due}. Puede aplicarse mora: paga lo antes posible o comunícate con administración."),
            _ => ("Pago vencido", $"{what} ({amount}) venció el {due}. Puede aplicarse mora: paga lo antes posible o comunícate con administración.")
        };
    }
}
