using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Features.Notification.Services;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>Thông báo của user đang đăng nhập.</summary>
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ApiControllerBase
{
    private readonly INotificationService _service;

    public NotificationsController(INotificationService service) => _service = service;

    /// <summary>Danh sách thông báo (paged). unreadOnly=true để chỉ lấy chưa đọc.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] PageRequest page, [FromQuery] bool? unreadOnly, CancellationToken ct)
        => ToActionResult(await _service.GetMyAsync(CurrentUserId, page, unreadOnly, ct));

    /// <summary>Số thông báo chưa đọc — dùng cho badge.</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
        => ToActionResult(await _service.GetUnreadCountAsync(CurrentUserId, ct));

    /// <summary>Đánh dấu một thông báo là đã đọc.</summary>
    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
        => ToActionResult(await _service.MarkReadAsync(CurrentUserId, id, ct));

    /// <summary>Đánh dấu tất cả thông báo là đã đọc.</summary>
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
        => ToActionResult(await _service.MarkAllReadAsync(CurrentUserId, ct));
}
