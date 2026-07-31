using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Sales.DTOs;
using FengDeskAI.Application.Features.Shipping.DTOs;
using FengDeskAI.Domain.Enums.Sales;

namespace FengDeskAI.Application.Features.Shipping.Services;

public interface IShippingService
{
    /// <summary>Tiếp nhận callback nhà vận chuyển: lưu webhook thô, cập nhật delivery + ghi progress log.</summary>
    Task<IServiceResult> ProcessWebhookAsync(ShippingWebhookRequest request, CancellationToken ct = default);

    /// <summary>Lịch sử tiến trình của một delivery (owner/staff store hoặc admin).</summary>
    Task<IServiceResult<List<DeliveryProgressLogResponse>>> GetProgressLogsAsync(Guid deliveryId, Guid userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>
    /// Yêu cầu nhà vận chuyển giao lại một delivery đang ở trạng thái giao thất bại (owner/staff store hoặc admin).
    /// Không tự đổi trạng thái — webhook tiếp theo sẽ cập nhật. Xem Documents/GHN_INTEGRATION.md §9.2.
    /// </summary>
    Task<IServiceResult> RedeliverAsync(Guid deliveryId, Guid userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>
    /// Cửa hàng đã đủ thông tin để tạo vận đơn chưa (địa chỉ lấy hàng, mã vùng, SĐT người gửi, mã shop).
    /// Trả kèm vai trò người gọi: owner/admin sửa được → có <c>Section</c> để FE điều hướng;
    /// garden staff chỉ nhận thông báo liên hệ chủ cửa hàng.
    /// </summary>
    Task<IServiceResult<StoreShippingReadinessResponse>> GetStoreReadinessAsync(Guid storeId, Guid userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>
    /// [Nhân viên sàn] Chạy ngay một lượt đồng bộ mã shop nhà vận chuyển thay vì chờ worker định kỳ.
    /// Chỉ gửi request cho store đủ điều kiện; store bị bỏ qua được trả về kèm lý do.
    /// </summary>
    Task<IServiceResult<CarrierShopSyncResultResponse>> SyncCarrierShopsAsync(int batchSize, CancellationToken ct = default);

    /// <summary>
    /// [Nhân viên sàn] Đồng bộ mã shop cho đúng một cửa hàng, trả về tình trạng sẵn sàng sau khi chạy
    /// để màn hình quản trị hiển thị ngay kết quả.
    /// </summary>
    Task<IServiceResult<StoreShippingReadinessResponse>> SyncStoreCarrierShopAsync(Guid storeId, CancellationToken ct = default);

    /// <summary>
    /// [CHỈ DEVELOPMENT] Giả lập callback của nhà vận chuyển cho một delivery. Đi qua CHÍNH
    /// <see cref="ProcessWebhookAsync"/> nên chạy đủ đường thật: lưu webhook thô, guard transition,
    /// mốc thời gian, progress log, rollup order, notification.
    /// <b>Tự đi qua các bước bắc cầu</b> mà state machine đòi hỏi (vd <c>Preparing → Shipped → Delivered</c>),
    /// nên gọi thẳng trạng thái đích là được.
    /// Cần thiết vì môi trường dev của GHN không có shipper — đơn nằm mãi ở <c>ready_to_pick</c>.
    /// </summary>
    Task<IServiceResult<CarrierSimulationResultResponse>> SimulateCarrierStatusAsync(
        Guid deliveryId, DeliveryStatus newStatus, CancellationToken ct = default);

    /// <summary>
    /// [CHỈ DEVELOPMENT] Giả lập nhà vận chuyển cho <b>TẤT CẢ</b> delivery của một order.
    /// Tự đi qua các bước trung gian cần thiết (vd <c>Preparing → Shipped → Delivered</c>) nên gọi
    /// một lần là đơn nhiều store chuyển hết, order rollup theo. KHÔNG lọc theo chủ đơn.
    /// </summary>
    Task<IServiceResult<List<CarrierSimulationResultResponse>>> SimulateCarrierStatusForOrderAsync(
        Guid orderId, DeliveryStatus newStatus, CancellationToken ct = default);

    /// <summary>
    /// Toàn bộ delivery của một order (đơn nhiều store → nhiều delivery). KHÔNG lọc theo chủ đơn,
    /// nên dev lấy được delivery id của bất kỳ đơn nào mà không phải đổi JWT.
    /// </summary>
    Task<IServiceResult<List<DeliveryResponse>>> GetDeliveriesByOrderAsync(Guid orderId, CancellationToken ct = default);
}
