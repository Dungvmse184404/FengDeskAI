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
            .ForMember(d => d.ProductId, o => o.MapFrom(s => s.ProductItem != null ? s.ProductItem.ProductId : Guid.Empty))
            .ForMember(d => d.LineTotal, o => o.MapFrom(s => s.UnitPrice * s.Quantity));

        CreateMap<Delivery, DeliveryResponse>()
            .ForMember(d => d.StoreName, o => o.MapFrom(s => s.Store != null ? s.Store.Name : null));

        CreateMap<Delivery, StoreDeliveryResponse>();
        CreateMap<OrderStatusLog, OrderStatusLogResponse>();

        CreateMap<Order, OrderListItemResponse>()
            .ForMember(d => d.DeliveryCount, o => o.MapFrom(s => s.Deliveries.Count))
            .ForMember(d => d.Stores, o => o.Ignore())
            .AfterMap((src, dest) => dest.Stores = BuildStores(src));

        CreateMap<Order, OrderDetailResponse>();
    }

    /// <summary>
    /// Cửa hàng của đơn: ưu tiên delivery (đã gom theo store). Đơn online chưa thanh toán chưa có
    /// delivery nên phải suy ra từ product của từng item. Lọc trùng vì nhiều item có thể cùng store.
    /// </summary>
    private static List<OrderStoreResponse> BuildStores(Order order)
    {
        var stores = order.Deliveries.Count > 0
            ? order.Deliveries.Select(d => (Id: d.GardenStoreId, Name: d.Store?.Name))
            : order.Items
                .Where(i => i.ProductItem?.Product is not null)
                .Select(i => (Id: i.ProductItem.Product.GardenStoreId, Name: i.ProductItem.Product.Store?.Name));

        return stores
            .GroupBy(s => s.Id)
            .Select(g => new OrderStoreResponse
            {
                StoreId = g.Key,
                StoreName = g.Select(s => s.Name).FirstOrDefault(n => n is not null),
            })
            .ToList();
    }
}
