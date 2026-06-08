using AutoMapper;
using FengDeskAI.Application.Features.Shipping.DTOs;
using FengDeskAI.Domain.Entities.Shipping;

namespace FengDeskAI.Application.Features.Shipping.Mappings;

public class ShippingMappingProfile : Profile
{
    public ShippingMappingProfile()
    {
        CreateMap<DeliveryProgressLog, DeliveryProgressLogResponse>();
    }
}
