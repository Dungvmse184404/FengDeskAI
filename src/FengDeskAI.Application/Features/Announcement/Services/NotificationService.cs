using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Announcement.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Announcement;

namespace FengDeskAI.Application.Features.Announcement.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public NotificationService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task CreateAsync(CreateNotificationRequest request, CancellationToken ct = default)
    {
        var noti = new Notification
        {
            UserId = request.UserId,
            Type = request.Type,
            Title = request.Title,
            Message = request.Message,
            ReferenceId = request.ReferenceId,
            ReferenceType = request.ReferenceType,
            IsRead = false,
        };
        await _uow.Notifications.AddAsync(noti, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IServiceResult<PagedResult<NotificationResponse>>> GetMyAsync(
        Guid userId, PageRequest page, bool? unreadOnly, CancellationToken ct = default)
    {
        var (items, total) = await _uow.Notifications.GetPagedByUserAsync(
            userId, page.Page, page.PageSize, unreadOnly, ct);

        var dtos = _mapper.Map<List<NotificationResponse>>(items);
        var result = new PagedResult<NotificationResponse>(dtos, page.Page, page.PageSize, total);
        return ServiceResult<PagedResult<NotificationResponse>>.Success(result);
    }

    public async Task<IServiceResult<int>> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        var count = await _uow.Notifications.CountUnreadAsync(userId, ct);
        return ServiceResult<int>.Success(count);
    }

    public async Task<IServiceResult> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        var notification = await _uow.Notifications.GetByIdAndUserAsync(notificationId, userId, ct);
        if (notification is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy thông báo.");

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync(ct);
        }

        return ServiceResult.Success("Đã đánh dấu đã đọc.");
    }

    public async Task<IServiceResult> MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        var unread = await _uow.Notifications.GetUnreadByUserAsync(userId, ct);
        if (unread.Count == 0)
            return ServiceResult.Success("Không có thông báo chưa đọc.");

        var now = DateTime.UtcNow;
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }

        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã đánh dấu {unread.Count} thông báo là đã đọc.");
    }
}
