using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Announcement;
using HSAcademia.Application.Interfaces;
using HSAcademia.Domain.Entities;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

public class AnnouncementService : IAnnouncementService
{
    private readonly AppDbContext _context;

    public AnnouncementService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<AnnouncementDto>> GetAnnouncementsAsync(Guid academyId)
    {
        var announcements = await _context.Announcements
            .Include(a => a.Author)
            .Where(a => a.AcademyId == academyId && !a.IsDeleted)
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.DatePosted)
            .ToListAsync();

        return announcements.Select(a => new AnnouncementDto
        {
            Id = a.Id,
            Title = a.Title,
            Content = a.Content,
            IsPinned = a.IsPinned,
            DatePosted = a.DatePosted,
            AuthorName = $"{a.Author.FirstName} {a.Author.LastName}"
        }).ToList();
    }

    public async Task<AnnouncementDto> CreateAnnouncementAsync(Guid academyId, Guid authorId, CreateAnnouncementDto dto)
    {
        var entity = new Announcement
        {
            AcademyId = academyId,
            AuthorId = authorId,
            Title = dto.Title,
            Content = dto.Content,
            IsPinned = dto.IsPinned,
            DatePosted = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Announcements.Add(entity);
        await _context.SaveChangesAsync();

        var created = await _context.Announcements
            .Include(a => a.Author)
            .FirstOrDefaultAsync(a => a.Id == entity.Id);

        return new AnnouncementDto
        {
            Id = created!.Id,
            Title = created.Title,
            Content = created.Content,
            IsPinned = created.IsPinned,
            DatePosted = created.DatePosted,
            AuthorName = $"{created.Author.FirstName} {created.Author.LastName}"
        };
    }

    public async Task DeleteAnnouncementAsync(Guid academyId, Guid announcementId)
    {
        var entity = await _context.Announcements
            .FirstOrDefaultAsync(a => a.AcademyId == academyId && a.Id == announcementId);

        if (entity == null)
            throw new KeyNotFoundException("Comunicado no encontrado.");

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }
}
