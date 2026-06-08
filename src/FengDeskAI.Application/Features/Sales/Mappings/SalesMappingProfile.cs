using AutoMapper;
using FengDeskAI.Application.Features.Sales.DTOs;
using FengDeskAI.Domain.Entities.Sales;

namespace FengDeskAI.Application.Features.Sales.Mappings;

/// <summary>Chỉ map chiều đọc (entity → response). Create/checkout dựng entity bằng tay trong service.</summary>
public class SalesMappingProfile : Profile
{
    public SalesMappingProfile()
    {
        CreateMap<CartItem, CartItemResponse>()
            .ForMember(d => d.ProductName, o => o.MapFrom(s => s.ProductItem.Product != null ? s.ProductItem.Product.Name : null))
            .ForMember(d => d.VariantName, o => o.MapFrom(s => s.ProductItem.Name))
            .ForMember(d => d.UnitPrice, o => o.MapFrom(s => s.ProductItem.Price))
            .ForMember(d => d.Stock, o => o.MapFrom(s => s.ProductItem.Stock))
            .ForMember(d => d.LineTotal, o => o.MapFrom(s => s.ProductItem.Price * s.Quantity));

        CreateMap<OrderItem, OrderItemResponse>()
            .ForMember(d => d.LineTotal, o => o.MapFrom(s => s.UnitPrice * s.Quantity));

        CreateMap<Delivery, DeliveryResponse>()
            .ForMember(d => d.StoreName, o => o.MapFrom(s => s.Store != null ? s.Store.Name : null));

        CreateMap<Delivery, StoreDeliveryResponse>();
        CreateMap<OrderStatusLog, OrderStatusLogResponse>();

        CreateMap<Order, OrderListItemResponse>()
            .ForMember(d => d.DeliveryCount, o => o.MapFrom(s => s.Deliveries.Count));

        CreateMap<Order, OrderDetailResponse>();
    }
}
