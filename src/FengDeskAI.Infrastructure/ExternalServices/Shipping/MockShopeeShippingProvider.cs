using FengDeskAI.Application.Interfaces.External;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.ExternalServices.Shipping;

/// <summary>
/// Giả lập nhà vận chuyển Shopee Express để chạy flow end-to-end khi chưa có credential thật.
/// Trả về tracking code + provider order id giả. Thay bằng impl gọi API Shopee thật sau.
/// </summary>
public class MockShopeeShippingProvider : IShippingProvider
{
    private readonly ILogger<MockShopeeShippingProvider> _logger;

    public MockShopeeShippingProvider(ILogger<MockShopeeShippingProvider> logger) => _logger = logger;

    public string Name => "ShopeeExpress";

    public Task<ShipmentResult> CreateShipmentAsync(ShipmentRequest request, CancellationToken ct = default)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var providerOrderId = $"SPX{suffix}";
        var trackingCode = $"VN{suffix}SPE";

        _logger.LogInformation(
            "[MockShopee] Tạo vận đơn cho delivery {DeliveryId}: tracking {Tracking}.",
            request.DeliveryId, trackingCode);

        return Task.FromResult(new ShipmentResult(
            Provider: Name,
            ProviderOrderId: providerOrderId,
            TrackingCode: trackingCode,
            EstimatedDeliveryDate: DateTime.UtcNow.AddDays(3)));
    }
}
