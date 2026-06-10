namespace FengDeskAI.Application.Features.Sales.Services;

/// <summary>Quét và chuyển các đơn online quá hạn thanh toán sang Expired (worker gọi định kỳ).</summary>
public interface IOrderExpirationService
{
    /// <summary>
    /// Chuyển các đơn Pending (không COD) tạo quá <paramref name="pendingTimeout"/> sang Expired:
    /// hủy link PayOS, transaction → Expired, delivery → Cancelled, hoàn kho, ghi log.
    /// Trả về số đơn đã xử lý.
    /// </summary>
    Task<int> ExpireOverdueOrdersAsync(TimeSpan pendingTimeout, CancellationToken ct = default);
}
