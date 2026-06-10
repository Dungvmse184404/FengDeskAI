using FengDeskAI.Application.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Application.Features.Sales.Services;

public class OrderExpirationService : IOrderExpirationService
{
    /// <summary>Số đơn tối đa xử lý mỗi lượt quét — phần còn lại để lượt sau, tránh giữ scope quá lâu.</summary>
    private const int BatchSize = 50;

    private readonly IUnitOfWork _uow;
    private readonly IOrderCancellationService _cancellation;
    private readonly ILogger<OrderExpirationService> _logger;

    public OrderExpirationService(IUnitOfWork uow, IOrderCancellationService cancellation, ILogger<OrderExpirationService> logger)
    {
        _uow = uow;
        _cancellation = cancellation;
        _logger = logger;
    }

    public async Task<int> ExpireOverdueOrdersAsync(TimeSpan pendingTimeout, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - pendingTimeout;
        var orders = await _uow.Orders.GetOverduePendingAsync(cutoff, BatchSize, ct);
        if (orders.Count == 0) return 0;

        var note = $"Đơn hết hạn thanh toán (quá {pendingTimeout.TotalMinutes:0} phút không thanh toán)";
        var expired = 0;
        foreach (var order in orders)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // Mỗi đơn một DB transaction riêng — một đơn lỗi không chặn các đơn còn lại.
                await _cancellation.CancelAsync(order, actorId: null, note, expired: true, ct);
                expired++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chuyển hết hạn thất bại cho order {OrderId} — sẽ thử lại ở lượt quét sau.", order.Id);
            }
        }

        _logger.LogInformation("Đã chuyển {Expired}/{Found} đơn quá hạn thanh toán sang Expired.", expired, orders.Count);
        return expired;
    }
}
