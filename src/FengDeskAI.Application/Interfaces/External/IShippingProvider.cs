namespace FengDeskAI.Application.Interfaces.External;

/// <summary>
/// Trừu tượng nhà vận chuyển (impl: GHN / AhaMove / MockShopee). Tạo vận đơn outbound khi order Paid,
/// ước tính phí ship lúc checkout, và yêu cầu giao lại đơn giao thất bại.
/// </summary>
public interface IShippingProvider
{
    string Name { get; }

    /// <summary>
    /// True nếu mỗi store BẮT BUỘC phải có mã shop riêng của nhà vận chuyển (không có giá trị mặc định
    /// để dùng chung). Dùng để cảnh báo store thiếu cấu hình trước khi tạo vận đơn. Mặc định: không cần.
    /// </summary>
    bool RequiresStoreShopId => false;

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

    /// <summary>
    /// Đăng ký điểm lấy hàng (shop) cho một garden store, trả về mã shop để lưu vào
    /// <c>GardenStore.GhnShopId</c>. Trả null nếu provider không hỗ trợ. Mặc định: không hỗ trợ.
    /// </summary>
    Task<int?> RegisterShopAsync(ShopRegistrationRequest request, CancellationToken ct = default)
        => Task.FromResult<int?>(null);
}
