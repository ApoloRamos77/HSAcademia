using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services.Push;

/// <summary>
/// Envía lo pendiente de avisos y comunicados. Se guardan junto con la operación que los origina y aquí se
/// despachan aparte: una caída de Firebase no afecta un cobro ni una lista, y lo pendiente se reintenta.
/// </summary>
public class PushDispatchService
{
    private const int BatchSize = 100;
    private const int MaxBodyLength = 180;
    /// <summary>Una llegada avisada horas después ya no sirve: lo vencido se descarta sin enviar.</summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(1);

    private readonly AppDbContext _db;
    private readonly IPushSender _sender;

    public PushDispatchService(AppDbContext db, IPushSender sender)
    {
        _db = db;
        _sender = sender;
    }

    public async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        await DispatchNoticesAsync(cancellationToken);
        await DispatchAnnouncementsAsync(cancellationToken);
    }

    private async Task DispatchNoticesAsync(CancellationToken cancellationToken)
    {
        var notices = await _db.FamilyNotices
            .Include(n => n.Student)
            .Where(n => n.PushedAt == null)
            .OrderBy(n => n.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var notice in notices)
        {
            if (DateTime.UtcNow - notice.CreatedAt <= MaxAge)
            {
                var recipients = new[] { notice.Student.GuardianId, notice.Student.UserId }
                    .Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
                var tokens = await _db.DeviceTokens.AsNoTracking()
                    .Where(d => recipients.Contains(d.UserId) && d.User.Status == UserStatus.Active)
                    .Select(d => d.Token)
                    .ToListAsync(cancellationToken);
                await SendAsync(tokens, new PushMessage(TypeOf(notice.Type), notice.Title, Shorten(notice.Body),
                    new Dictionary<string, string> { ["type"] = TypeOf(notice.Type), ["studentId"] = notice.StudentId.ToString() }), cancellationToken);
            }

            notice.PushedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task DispatchAnnouncementsAsync(CancellationToken cancellationToken)
    {
        var announcements = await _db.Announcements
            .Where(a => a.PushedAt == null)
            .OrderBy(a => a.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var announcement in announcements)
        {
            if (DateTime.UtcNow - announcement.CreatedAt <= MaxAge)
            {
                var tokens = await _db.DeviceTokens.AsNoTracking()
                    .Where(d => d.AcademyId == announcement.AcademyId && d.UserId != announcement.AuthorId && d.User.Status == UserStatus.Active)
                    .Select(d => d.Token)
                    .ToListAsync(cancellationToken);
                await SendAsync(tokens, new PushMessage("announcement", announcement.Title, Shorten(announcement.Content),
                    new Dictionary<string, string> { ["type"] = "announcement" }), cancellationToken);
            }

            announcement.PushedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SendAsync(List<string> tokens, PushMessage message, CancellationToken cancellationToken)
    {
        if (tokens.Count == 0) return;
        var invalid = await _sender.SendAsync(tokens, message, cancellationToken);
        if (invalid.Count > 0)
            await _db.DeviceTokens.Where(d => invalid.Contains(d.Token)).ExecuteDeleteAsync(cancellationToken);
    }

    private static string TypeOf(FamilyNoticeType type) => type switch
    {
        FamilyNoticeType.Arrival => "arrival",
        FamilyNoticeType.PaymentProof => "proof",
        FamilyNoticeType.EventCall => "call",
        _ => "notice"
    };

    private static string Shorten(string text) =>
        text.Length <= MaxBodyLength ? text : $"{text[..(MaxBodyLength - 1)].TrimEnd()}…";
}
