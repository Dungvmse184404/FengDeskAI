using AutoMapper;
using FengDeskAI.Application.Features.Returns.DTOs;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;

namespace FengDeskAI.Application.Features.Returns.Mappings;

/// <summary>Map chiều đọc (entity → response) cho luồng RMA/hoàn tiền. Tạo entity dựng tay trong service.</summary>
public class ReturnMappingProfile : Profile
{
    public ReturnMappingProfile()
    {
        CreateMap<ReturnItem, ReturnItemResponse>()
            .ForMember(d => d.ProductName, o => o.MapFrom(s => s.OrderItem != null ? s.OrderItem.ProductName : null))
            .ForMember(d => d.LineTotal, o => o.MapFrom(s => s.UnitPrice * s.Quantity))
            .ForMember(d => d.ExchangeProductName, o => o.MapFrom(s =>
                s.ExchangeProductItem != null && s.ExchangeProductItem.Product != null
                    ? s.ExchangeProductItem.Product.Name
                    : null))
            .ForMember(d => d.ExchangeVariantName, o => o.MapFrom(s =>
                s.ExchangeProductItem != null ? s.ExchangeProductItem.Name : null))
            .ForMember(d => d.ExchangeUnitPrice, o => o.MapFrom(s =>
                s.ExchangeProductItem != null ? (decimal?)s.ExchangeProductItem.Price : null))
            .ForMember(d => d.ExchangeLineTotal, o => o.MapFrom(s =>
                s.ExchangeProductItem != null ? (decimal?)(s.ExchangeProductItem.Price * s.Quantity) : null))
            .ForMember(d => d.ExchangeImageUrl, o => o.MapFrom(s =>
                s.ExchangeProductItem != null && s.ExchangeProductItem.Product != null
                    ? s.ExchangeProductItem.Product.Images
                        .OrderBy(i => i.SortOrder)
                        .Select(i => i.Url)
                        .FirstOrDefault()
                    : null));

        CreateMap<Refund, RefundResponse>();
        CreateMap<VendorLiability, VendorLiabilityResponse>();
        CreateMap<ReturnStatusLog, ReturnStatusLogResponse>();

        CreateMap<ReturnRequest, ReturnListItemResponse>()
            .ForMember(d => d.ItemCount, o => o.MapFrom(s => s.Items.Count));

        CreateMap<ReturnRequest, ReturnDetailResponse>()
            .ForMember(d => d.ImageUrls, o => o.MapFrom(s => s.Images.Select(i => i.ImageUrl).ToList()));
    }
}
