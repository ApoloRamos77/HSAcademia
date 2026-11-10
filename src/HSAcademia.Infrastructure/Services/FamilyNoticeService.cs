using HSAcademia.Application.DTOs.Attendance;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Avisos a la familia dentro de la app. Cada aviso nuevo se envía además como notificación
/// (PushDispatchService); corregir una marca antes del envío lo retira y no llega a salir.
/// </summary>
public class FamilyNoticeService
{
    private const int MaxListed = 30;
    private const int RetentionDays = 30;

    private readonly AppDbContext _db;

    public FamilyNoticeService(AppDbContext db) => _db = db;

    /// <summary>
    /// Deja los avisos de llegada de una sesión de hoy al día con sus marcas. No guarda.
    /// Corregir una marca a falta retira el aviso. Las listas pasadas tarde no avisan a nadie.
    /// </summary>
    public async Task SyncArrivalsAsync(Guid academyId, Event session, IReadOnlyCollection<Attendance> marks)
    {
        if (session.StartTime.Date != AcademyClock.Today || marks.Count == 0) return;

        var studentIds = marks.Select(m => m.StudentId).Distinct().ToList();
        var existing = await _db.FamilyNotices
            .Where(n => n.AcademyId == academyId && n.EventId == session.Id &&
                        n.Type == FamilyNoticeType.Arrival && studentIds.Contains(n.StudentId))
            .ToListAsync();
        var names = await _db.Students.AsNoTracking()
            .Where(s => s.AcademyId == academyId && studentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.FirstName);

        foreach (var mark in marks)
        {
            var notice = existing.FirstOrDefault(n => n.StudentId == mark.StudentId);
            var arrived = mark.Status is AttendanceStatus.Present or AttendanceStatus.Late;
            if (arrived && notice == null)
            {
                _db.FamilyNotices.Add(new FamilyNotice
                {
                    AcademyId = academyId,
                    StudentId = mark.StudentId,
                    EventId = session.Id,
                    Type = FamilyNoticeType.Arrival,
                    Title = $"{names.GetValueOrDefault(mark.StudentId, "Tu hijo/a")} llegó",
                    Body = $"Asistencia registrada en {session.Title}."
                });
            }
            else if (!arrived && notice != null)
            {
                _db.FamilyNotices.Remove(notice);
            }
        }
    }

    public async Task<List<FamilyNoticeDto>> ListMineAsync(Guid academyId, Guid userId)
    {
        var since = DateTime.UtcNow.AddDays(-RetentionDays);
        return await MineQuery(academyId, userId)
            .Where(n => n.CreatedAt >= since)
            .OrderByDescending(n => n.CreatedAt)
            .Take(MaxListed)
            .Select(n => new FamilyNoticeDto
            {
                Id = n.Id,
                StudentId = n.StudentId,
                Title = n.Title,
                Body = n.Body,
                CreatedAt = n.CreatedAt,
                IsRead = n.ReadAt != null
            })
            .ToListAsync();
    }

    public Task MarkAllReadAsync(Guid academyId, Guid userId) =>
        MineQuery(academyId, userId)
            .Where(n => n.ReadAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(n => n.ReadAt, DateTime.UtcNow));

    private IQueryable<FamilyNotice> MineQuery(Guid academyId, Guid userId) =>
        _db.FamilyNotices.Where(n => n.AcademyId == academyId &&
                                     (n.Student.UserId == userId || n.Student.GuardianId == userId));
}
