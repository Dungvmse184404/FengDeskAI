namespace FengDeskAI.Application.Interfaces.External;

/// <summary>
/// Yêu cầu tạo vận đơn cho một <c>Delivery</c> (phần hàng của 1 store trong order).
/// Mang đủ thông tin điểm lấy hàng (store) + điểm giao (khách) + danh sách hàng để
/// provider (vd AhaMove) gọi API mà KHÔNG cần truy cập DB — tầng Application gom dữ liệu.
/// </summary>
public record ShipmentRequest(
    Guid DeliveryId,
    Guid OrderId,
    decimal Subtotal,
    // --- điểm lấy hàng (StoreAddress) ---
    string PickupName,
    string PickupPhone,
    string PickupAddress,
    decimal? PickupLat,
    decimal? PickupLng,
    string? ServiceId,           // GardenStore.AhamoveServiceId, vd "SGN-BIKE" (null → provider tự chọn theo điểm lấy)
    // --- điểm giao (khách) ---
    string RecipientName,
    string RecipientPhone,
    string ShippingAddress,
    decimal? RecipientLat,
    decimal? RecipientLng,
    decimal CodAmount,           // 0 khi đã thu tiền online; > 0 khi COD
    int TotalWeightGram,
    IReadOnlyList<ShipmentItem> Items,
    // --- GHN (định tuyến theo mã quận/phường, không dùng lat/lng) ---
    int? ShopId = null,          // GardenStore.GhnShopId (điểm lấy); null → DefaultShopId của cấu hình
    int? ServiceTypeId = null,   // GHN: 2 = chuyển phát nhanh (nhẹ), 5 = chuyển phát thường (nặng)
    int? FromDistrictId = null,  // mã quận/huyện GHN của store
    string? FromWardCode = null, // mã phường/xã GHN của store
    int? ToDistrictId = null,    // mã quận/huyện GHN của khách
    string? ToWardCode = null,   // mã phường/xã GHN của khách
    int LengthCm = 10,           // kích thước kiện (tổng hợp từ items)
    int WidthCm = 10,
    int HeightCm = 10);

public record ShipmentItem(
    string Id, string Name, decimal Price, int Quantity,
    int WeightGram = 500, int LengthCm = 10, int WidthCm = 10, int HeightCm = 10);

public record ShipmentResult(
    string Provider,
    string ProviderOrderId,
    string TrackingCode,
    DateTime? EstimatedDeliveryDate,
    string? TrackingUrl = null,  // shared_link AhaMove / link tra cứu GHN (theo dõi công khai)
    decimal? ShippingFee = null); // phí ship thực tế nhà vận chuyển trả về (vd total_fee của GHN)
