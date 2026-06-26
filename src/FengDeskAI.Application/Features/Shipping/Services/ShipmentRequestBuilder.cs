using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Domain.Entities.Vendor;

namespace FengDeskAI.Application.Features.Shipping.Services;

/// <summary>
/// Dựng <see cref="ShipmentRequest"/> từ store (điểm lấy) + địa chỉ khách (điểm giao). Gom phần
/// boilerplate dùng chung cho mọi điểm tạo vận đơn (thanh toán, đổi hàng, store xác nhận COD).
/// </summary>
public static class ShipmentRequestBuilder
{
    public static ShipmentRequest Build(
        Guid deliveryId, Guid orderId, decimal subtotal,
        GardenStore? store, UserAddress? shipTo,
        decimal codAmount, int totalWeightGram, IReadOnlyList<ShipmentItem> items)
    {
        var pickup = store?.Address;
        return new ShipmentRequest(
            DeliveryId: deliveryId,
            OrderId: orderId,
            Subtotal: subtotal,
            PickupName: pickup?.SenderName ?? store?.Name ?? string.Empty,
            PickupPhone: pickup?.SenderPhone ?? store?.Hotline ?? string.Empty,
            PickupAddress: ShipmentAddressFormatter.Compose(pickup?.StreetAddress, pickup?.Ward),
            PickupLat: pickup?.Latitude,
            PickupLng: pickup?.Longitude,
            ServiceId: store?.AhamoveServiceId,
            RecipientName: shipTo?.RecipientName ?? string.Empty,
            RecipientPhone: shipTo?.RecipientPhone ?? string.Empty,
            ShippingAddress: ShipmentAddressFormatter.Compose(shipTo?.StreetAddress, shipTo?.Ward),
            RecipientLat: shipTo?.Latitude,
            RecipientLng: shipTo?.Longitude,
            CodAmount: codAmount,
            TotalWeightGram: totalWeightGram,
            Items: items,
            // GHN: mã vùng điểm lấy (store) + điểm giao (khách) + kích thước kiện tổng hợp.
            ShopId: store?.GhnShopId,
            ServiceTypeId: store?.GhnServiceTypeId,
            FromDistrictId: pickup?.Ward?.District?.GhnDistrictId,
            FromWardCode: pickup?.Ward?.GhnWardCode,
            ToDistrictId: shipTo?.Ward?.District?.GhnDistrictId,
            ToWardCode: shipTo?.Ward?.GhnWardCode,
            LengthCm: AggregateDim(items, i => i.LengthCm, sum: false),
            WidthCm: AggregateDim(items, i => i.WidthCm, sum: false),
            HeightCm: AggregateDim(items, i => i.HeightCm, sum: true));
    }

    /// <summary>
    /// Gộp kích thước kiện hàng cho GHN: dài/rộng lấy max, cao cộng dồn (xếp chồng) theo số lượng,
    /// chặn trong [10, 200] cm. Đủ dùng khi chưa tính đóng gói chính xác.
    /// </summary>
    private static int AggregateDim(IReadOnlyList<ShipmentItem> items, Func<ShipmentItem, int> pick, bool sum)
    {
        if (items.Count == 0) return 10;
        var value = sum
            ? items.Sum(i => pick(i) * i.Quantity)
            : items.Max(pick);
        return Math.Clamp(value, 10, 200);
    }
}
