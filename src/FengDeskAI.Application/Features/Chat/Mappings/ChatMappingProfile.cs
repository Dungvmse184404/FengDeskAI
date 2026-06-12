using AutoMapper;
using FengDeskAI.Application.Features.Chat.DTOs;
using ChatboxEntity = FengDeskAI.Domain.Entities.Chat.Chatbox;
using ChatMessageEntity = FengDeskAI.Domain.Entities.Chat.ChatMessage;

namespace FengDeskAI.Application.Features.Chat.Mappings;

public class ChatMappingProfile : Profile
{
    public ChatMappingProfile()
    {
        CreateMap<ChatboxEntity, ChatboxResponse>()
            .ForMember(d => d.LastMessage, opt =>
                opt.MapFrom(s => s.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault()));

        CreateMap<ChatMessageEntity, ChatMessageResponse>();
    }
}
