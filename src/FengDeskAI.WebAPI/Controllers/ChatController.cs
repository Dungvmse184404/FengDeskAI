using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Features.Chat.DTOs;
using FengDeskAI.Application.Features.Chat.Services;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>Chat realtime giữa 2 người dùng.</summary>
[Route("api/chat")]
[Authorize]
public class ChatController : ApiControllerBase
{
    private readonly IChatService _service;

    public ChatController(IChatService service) => _service = service;

    /// <summary>Lấy hoặc tạo chatbox với user khác.</summary>
    [HttpPost("chatbox/with/{otherUserId:guid}")]
    public async Task<IActionResult> GetOrStart(Guid otherUserId, CancellationToken ct)
        => ToActionResult(await _service.GetOrStartAsync(CurrentUserId, otherUserId, ct));

    /// <summary>Danh sách chatbox của tôi (paged).</summary>
    [HttpGet("chatboxes")]
    public async Task<IActionResult> GetMine([FromQuery] PageRequest page, CancellationToken ct)
        => ToActionResult(await _service.GetMineAsync(CurrentUserId, page, ct));

    /// <summary>Lấy messages trong chatbox (paged, mới nhất trước).</summary>
    [HttpGet("chatbox/{chatboxId:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid chatboxId, [FromQuery] PageRequest page, CancellationToken ct)
        => ToActionResult(await _service.GetMessagesAsync(CurrentUserId, chatboxId, page, ct));

    /// <summary>Gửi message.</summary>
    [HttpPost("chatbox/{chatboxId:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid chatboxId, [FromBody] SendMessageRequest request, CancellationToken ct)
        => ToActionResult(await _service.SendMessageAsync(CurrentUserId, chatboxId, request, ct));

    /// <summary>Đánh dấu 1 message đã đọc.</summary>
    [HttpPatch("message/{messageId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid messageId, CancellationToken ct)
        => ToActionResult(await _service.MarkAsReadAsync(CurrentUserId, messageId, ct));

    /// <summary>Đánh dấu tất cả message trong chatbox đã đọc.</summary>
    [HttpPatch("chatbox/{chatboxId:guid}/read-all")]
    public async Task<IActionResult> MarkChatboxAsRead(Guid chatboxId, CancellationToken ct)
        => ToActionResult(await _service.MarkChatboxAsReadAsync(CurrentUserId, chatboxId, ct));
}
