using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Payment;

namespace FengDeskAI.Application.Features.Returns.Services;

/// <summary>Xử lý phần tiền của luồng trả hàng — tạo lệnh hoàn tiền + gọi cổng + xác nhận hoàn tất.</summary>
public interface IRefundService
{
    /// <summary>
    /// Tạo lệnh hoàn tiền cho yêu cầu trả (thêm vào context, PHẢI gọi bên trong transaction).
    /// Đơn PayOS đã thanh toán → gọi cổng hoàn về nguồn (Processing); COD → giữ Pending chờ chuyển khoản.
    /// </summary>
    Task<Refund> CreateRefundAsync(ReturnRequest request, decimal amount, RefundMethod method, string reason, CancellationToken ct = default);

    /// <summary>Đánh dấu đã hoàn tiền thành công (Admin xác nhận). PHẢI gọi bên trong transaction.</summary>
    void Complete(Refund refund, Guid? actorId);
}
