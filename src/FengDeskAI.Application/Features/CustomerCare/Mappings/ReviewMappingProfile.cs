using AutoMapper;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Domain.Entities.CustomerCare;

namespace FengDeskAI.Application.Features.CustomerCare.Mappings;

public class ReviewMappingProfile : Profile
{
    public ReviewMappingProfile()
    {
        CreateMap<Review, ReviewResponse>();

        CreateMap<CreateReviewRequest, Review>()
            .ForMember(d => d.Id, opt => opt.Ignore())
            .ForMember(d => d.User, opt => opt.Ignore())
            .ForMember(d => d.Product, opt => opt.Ignore())
            .ForMember(d => d.CreatedBy, opt => opt.Ignore())
            .ForMember(d => d.UpdatedBy, opt => opt.Ignore())
            .ForMember(d => d.IsDeleted, opt => opt.Ignore());

        CreateMap<Review, CreateReviewRespond>();

        CreateMap<Review, UpdateReviewRespond>();
    }
}
