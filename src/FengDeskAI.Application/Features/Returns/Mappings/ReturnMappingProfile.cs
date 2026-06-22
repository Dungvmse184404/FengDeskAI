using AutoMapper;
using FengDeskAI.Application.Features.Returns.DTOs;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;

namespace FengDeskAI.Application.Features.Returns.Mappings;

/// <summary>Map chiều đọc (entity → response) cho luồng trả hàng/hoàn tiền. Tạo entity dựng tay trong service.</summary>
public class ReturnMappingProfile : Profile
{
    public ReturnMappingProfile()
    {
        CreateMap<ReturnItem, ReturnItemResponse>()
            .ForMember(d => d.ProductName, o => o.MapFrom(s => s.OrderItem != null ? s.OrderItem.ProductName : null))
            .ForMember(d => d.LineTotal, o => o.MapFrom(s => s.UnitPrice * s.Quantity));

        CreateMap<Refund, RefundResponse>();
        CreateMap<ReturnStatusLog, ReturnStatusLogResponse>();

        CreateMap<ReturnRequest, ReturnListItemResponse>()
            .ForMember(d => d.ItemCount, o => o.MapFrom(s => s.Items.Count));

        CreateMap<ReturnRequest, ReturnDetailResponse>()
            .ForMember(d => d.ImageUrls, o => o.MapFrom(s => s.Images.Select(i => i.ImageUrl).ToList()));
    }
}
