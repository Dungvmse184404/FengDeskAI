namespace FengDeskAI.Application.Interfaces.External;

/// <summary>
/// Trừu tượng nhà vận chuyển (impl hiện tại: MockShopee). Tạo vận đơn outbound khi order Paid.
/// </summary>
public interface IShippingProvider
{
    string Name { get; }
    Task<ShipmentResult> CreateShipmentAsync(ShipmentRequest request, CancellationToken ct = default);
}
