using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Announcement;

namespace HSAcademia.Application.Interfaces;

public interface IAnnouncementService
{
    Task<List<AnnouncementDto>> GetAnnouncementsAsync(Guid academyId);
    Task<AnnouncementDto> CreateAnnouncementAsync(Guid academyId, Guid authorId, CreateAnnouncementDto dto);
    Task DeleteAnnouncementAsync(Guid academyId, Guid announcementId);
}
