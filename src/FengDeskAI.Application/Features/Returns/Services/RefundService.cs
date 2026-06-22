using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Payment;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Application.Features.Returns.Services;

public class RefundService : IRefundService
{
    private readonly IUnitOfWork _uow;
    private readonly IPaymentGateway _gateway;
    private readonly ILogger<RefundService> _logger;

    public RefundService(IUnitOfWork uow, IPaymentGateway gateway, ILogger<RefundService> logger)
    {
        _uow = uow;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<Refund> CreateRefundAsync(ReturnRequest request, decimal amount, RefundMethod method, string reason, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var refund = new Refund
        {
            ReturnRequestId = request.Id,
            OrderId = request.OrderId,
            Amount = amount,
            Method = method,
            Status = RefundStatus.Pending,
            Note = reason,
        };

        // Hoàn về nguồn: cần giao dịch PayOS đã thanh toán để gọi cổng.
        if (method == RefundMethod.Original)
        {
            var txn = await _uow.Transactions.GetLatestByOrderAsync(request.OrderId, ct);
            if (txn is not null && txn.Status == PaymentStatus.Paid)
            {
                refund.TransactionId = txn.Id;
                try
                {
                    var roundedAmount = (int)Math.Round(amount, MidpointRounding.AwayFromZero);
                    var result = await _gateway.RefundAsync(new RefundRequest(txn.OrderCode, roundedAmount, reason), ct);
                    if (result.Success)
                    {
                        refund.Status = RefundStatus.Processing;
                        refund.ProviderRefundId = result.ProviderRefundId;
                        refund.ProcessedAt = now;
                    }
                    else
                    {
                        _logger.LogWarning("Hoàn tiền cổng thanh toán không thành công cho order {OrderId}: {Code} {Message}",
                            request.OrderId, result.Code, result.Message);
                    }
                }
                catch (Exception ex)
                {
                    // Lỗi cổng → giữ Pending để Admin xử lý thủ công, không chặn luồng nghiệp vụ.
                    _logger.LogError(ex, "Gọi hoàn tiền cổng thanh toán thất bại cho order {OrderId} — giữ trạng thái Pending.", request.OrderId);
                }
            }
        }

        await _uow.Returns.AddRefundAsync(refund, ct);
        return refund;
    }

    public void Complete(Refund refund, Guid? actorId)
    {
        var now = DateTime.UtcNow;
        refund.Status = RefundStatus.Completed;
        refund.ProcessedBy = actorId;
        refund.ProcessedAt ??= now;
        refund.CompletedAt = now;
    }
}
