using AutoMapper;
using FengDeskAI.Application.Features.Identity.DTOs;
using FengDeskAI.Domain.Entities.Identity;

namespace FengDeskAI.Application.Features.Identity.Mappings;

public class IdentityProfile : Profile
{
    public IdentityProfile()
    {
        CreateMap<User, UserSummary>()
            .ForMember(d => d.Role, opt => opt.MapFrom(s => s.Role.ToString()));
    }
}
