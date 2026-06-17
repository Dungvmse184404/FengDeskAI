using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Chat.DTOs;
using FengDeskAI.Application.Features.Chat.Services;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Services;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Chat: người ↔ người (customer ↔ garden owner / staff / manager...) và người ↔ trợ lý AI.
/// Cùng một mô hình chatboxes/chat_messages. AI có thể đọc ngữ cảnh sản phẩm để hỗ trợ.
/// </summary>
[Route("api/chat")]
[Authorize]
public class ChatController : ApiControllerBase
{
    private readonly IChatService _service;
    private readonly IAiChatService _aiService;

    public ChatController(IChatService service, IAiChatService aiService)
    {
        _service = service;
        _aiService = aiService;
    }

    // ───────────── Người ↔ người ─────────────

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

    /// <summary>Gửi message (text và/hoặc link ảnh).</summary>
    [HttpPost("chatbox/{chatboxId:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid chatboxId, [FromBody] SendMessageRequest request, CancellationToken ct)
        => ToActionResult(await _service.SendMessageAsync(
            CurrentUserId, CurrentUser.Role, CurrentUser.Email, chatboxId, request, ct));

    /// <summary>Tải ảnh chat lên storage (multipart, field "file") → trả link để gắn vào tin nhắn.</summary>
    [HttpPost("chatbox/{chatboxId:guid}/images")]
    public async Task<IActionResult> UploadImage(Guid chatboxId, IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return ToActionResult(ServiceResult<string>.Failure(ApiStatusCodes.BadRequest, "Vui lòng chọn tệp ảnh."));

        await using var stream = file.OpenReadStream();
        return ToActionResult(await _service.UploadImageAsync(
            CurrentUserId, chatboxId, stream, file.FileName, file.ContentType, ct));
    }

    /// <summary>Đánh dấu 1 message đã đọc.</summary>
    [HttpPatch("message/{messageId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid messageId, CancellationToken ct)
        => ToActionResult(await _service.MarkAsReadAsync(CurrentUserId, messageId, ct));

    /// <summary>Đánh dấu tất cả message trong chatbox đã đọc.</summary>
    [HttpPatch("chatbox/{chatboxId:guid}/read-all")]
    public async Task<IActionResult> MarkChatboxAsRead(Guid chatboxId, CancellationToken ct)
        => ToActionResult(await _service.MarkChatboxAsReadAsync(CurrentUserId, chatboxId, ct));

    // ───────────── Người ↔ AI ─────────────

    /// <summary>
    /// Gửi tin nhắn cho trợ lý AI. Bỏ trống ChatboxId ở lượt đầu (kèm ProductId nếu hỏi về sản phẩm) →
    /// server tạo hội thoại AI và trả lại ChatboxId để dùng cho lượt sau. Nhớ N lượt gần nhất.
    /// </summary>
    [HttpPost("ai/messages")]
    public async Task<IActionResult> SendToAi([FromBody] AiChatRequest request, CancellationToken ct)
        => ToActionResult(await _aiService.SendAsync(
            CurrentUserId, CurrentUser.Role, CurrentUser.Email, CurrentUser.Name, request, ct));
}
