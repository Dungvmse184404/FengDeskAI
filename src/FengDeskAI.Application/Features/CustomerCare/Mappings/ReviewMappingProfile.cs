using AutoMapper;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Entities.Identity;

namespace FengDeskAI.Application.Features.CustomerCare.Mappings;

public class ReviewMappingProfile : Profile
{
    public ReviewMappingProfile()
    {
        CreateMap<User, ReviewerResponse>();

        CreateMap<Review, ReviewResponse>()
            .ForMember(d => d.VariantName, opt => opt.MapFrom(s => s.OrderItem != null ? s.OrderItem.VariantName : null));

        CreateMap<Review, CreateReviewRespond>();

        CreateMap<Review, UpdateReviewRespond>();
    }
}
