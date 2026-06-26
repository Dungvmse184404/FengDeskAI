namespace FengDeskAI.Application.Interfaces.External;

/// <summary>
/// Trừu tượng nhà vận chuyển (impl: GHN / AhaMove / MockShopee). Tạo vận đơn outbound khi order Paid,
/// ước tính phí ship lúc checkout, và yêu cầu giao lại đơn giao thất bại.
/// </summary>
public interface IShippingProvider
{
    string Name { get; }

    Task<ShipmentResult> CreateShipmentAsync(ShipmentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Ước tính phí ship cho một delivery (VND). Trả null nếu provider không hỗ trợ ước tính →
    /// caller tự fallback sang <see cref="IShippingFeeCalculator"/>. Mặc định: không hỗ trợ.
    /// </summary>
    Task<decimal?> EstimateFeeAsync(ShipmentRequest request, CancellationToken ct = default)
        => Task.FromResult<decimal?>(null);

    /// <summary>
    /// Yêu cầu nhà vận chuyển giao lại một vận đơn (sau khi giao thất bại). Trả false nếu không hỗ trợ.
    /// KHÔNG tự đổi trạng thái delivery — để webhook tiếp theo cập nhật. Mặc định: không hỗ trợ.
    /// </summary>
    Task<bool> RedeliverAsync(string providerOrderCode, int? shopId, CancellationToken ct = default)
        => Task.FromResult(false);
}
