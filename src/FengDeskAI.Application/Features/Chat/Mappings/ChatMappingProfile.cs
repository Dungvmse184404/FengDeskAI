using AutoMapper;
using FengDeskAI.Application.Features.Chat.DTOs;
using FengDeskAI.Domain.Entities.Chat;

namespace FengDeskAI.Application.Features.Chat.Mappings;

public class ChatMappingProfile : Profile
{
    public ChatMappingProfile()
    {
        CreateMap<Chatbox, ChatboxResponse>()
            .ForMember(d => d.LastMessage, opt =>
                opt.MapFrom(s => s.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault()));

        CreateMap<ChatboxParticipant, ChatParticipantResponse>();

        CreateMap<ChatMessage, ChatMessageResponse>()
            .ForMember(d => d.Images, opt =>
                opt.MapFrom(s => s.Images.OrderBy(i => i.SortOrder).Select(i => i.Url)));
    }
}
