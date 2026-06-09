namespace FengDeskAI.Application.Interfaces.External;

public record ShipmentRequest(
    Guid DeliveryId,
    Guid OrderId,
    decimal Subtotal,
    string? RecipientName,
    string? RecipientPhone,
    string? ShippingAddress);

public record ShipmentResult(
    string Provider,
    string ProviderOrderId,
    string TrackingCode,
    DateTime? EstimatedDeliveryDate);

/// <summary>
/// Trừu tượng nhà vận chuyển (impl hiện tại: MockShopee). Tạo vận đơn outbound khi order Paid.
/// </summary>
public interface IShippingProvider
{
    string Name { get; }
    Task<ShipmentResult> CreateShipmentAsync(ShipmentRequest request, CancellationToken ct = default);
}
