using AutoMapper;
using FengDeskAI.Application.Features.Promotion.DTOs;
using FengDeskAI.Domain.Entities.Promotion;

namespace FengDeskAI.Application.Features.Promotion.Mappings;

public class PromotionMappingProfile : Profile
{
    public PromotionMappingProfile()
    {
        CreateMap<Voucher, VoucherResponse>();
    }
}
