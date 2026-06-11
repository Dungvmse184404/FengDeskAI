using AutoMapper;
using FengDeskAI.Application.Features.Notification.DTOs;
using NotificationEntity = FengDeskAI.Domain.Entities.Notification.Notification;

namespace FengDeskAI.Application.Features.Notification.Mappings;

public class NotificationMappingProfile : Profile
{
    public NotificationMappingProfile()
    {
        CreateMap<NotificationEntity, NotificationResponse>();
    }
}
