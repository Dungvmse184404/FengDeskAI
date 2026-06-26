using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Domain.Entities.Vendor;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Application.Features.Shipping.Services;

/// <summary>
/// Ước tính phí ship cho một delivery: ưu tiên gọi nhà vận chuyển (vd GHN /fee), nếu provider không
/// hỗ trợ hoặc lỗi (chưa đồng bộ mã vùng, GHN sập…) thì fallback sang <see cref="IShippingFeeCalculator"/>
/// để checkout không bao giờ vỡ. Xem Documents/GHN_INTEGRATION.md §4.3.
/// </summary>
public interface IDeliveryFeeEstimator
{
    Task<decimal> EstimateAsync(
        GardenStore? store, UserAddress? shipTo, decimal subtotal,
        int totalWeightGram, IReadOnlyList<ShipmentItem> items, CancellationToken ct = default);
}

public class DeliveryFeeEstimator : IDeliveryFeeEstimator
{
    private readonly IShippingProvider _shipping;
    private readonly IShippingFeeCalculator _calculator;
    private readonly ILogger<DeliveryFeeEstimator> _logger;

    public DeliveryFeeEstimator(IShippingProvider shipping, IShippingFeeCalculator calculator, ILogger<DeliveryFeeEstimator> logger)
    {
        _shipping = shipping;
        _calculator = calculator;
        _logger = logger;
    }

    public async Task<decimal> EstimateAsync(
        GardenStore? store, UserAddress? shipTo, decimal subtotal,
        int totalWeightGram, IReadOnlyList<ShipmentItem> items, CancellationToken ct = default)
    {
        // deliveryId/orderId không cần cho ước tính phí → Guid.Empty.
        var request = ShipmentRequestBuilder.Build(
            Guid.Empty, Guid.Empty, subtotal, store, shipTo, codAmount: 0m, totalWeightGram, items);

        try
        {
            var fee = await _shipping.EstimateFeeAsync(request, ct);
            if (fee is { } value && value >= 0) return value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ShippingFee] Ước tính qua {Provider} thất bại — dùng calculator nội bộ.", _shipping.Name);
        }

        return _calculator.Calculate(
            store?.Address?.Ward?.District?.ProvinceId,
            shipTo?.Ward?.District?.ProvinceId,
            totalWeightGram);
    }
}
