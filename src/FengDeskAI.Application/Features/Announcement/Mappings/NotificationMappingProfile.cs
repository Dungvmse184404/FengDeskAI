using AutoMapper;
using FengDeskAI.Application.Features.Announcement.DTOs;
using FengDeskAI.Domain.Entities.Announcement;

namespace FengDeskAI.Application.Features.Announcement.Mappings;

public class NotificationMappingProfile : Profile
{
    public NotificationMappingProfile()
    {
        CreateMap<Notification, NotificationResponse>();
    }
}
