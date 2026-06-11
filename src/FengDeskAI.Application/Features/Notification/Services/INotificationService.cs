using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Notification.DTOs;

namespace FengDeskAI.Application.Features.Notification.Services;

public interface INotificationService
{
    /// <summary>Tạo thông báo — gọi nội bộ từ OrderService, ShippingService, PaymentService.</summary>
    Task CreateAsync(CreateNotificationRequest request, CancellationToken ct = default);

    Task<IServiceResult<PagedResult<NotificationResponse>>> GetMyAsync(
        Guid userId, PageRequest page, bool? unreadOnly, CancellationToken ct = default);

    Task<IServiceResult<int>> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);

    Task<IServiceResult> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default);

    Task<IServiceResult> MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}
