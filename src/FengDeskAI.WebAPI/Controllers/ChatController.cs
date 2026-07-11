using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Chat.DTOs;
using FengDeskAI.Application.Features.Chat.Services;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Services;
using FengDeskAI.Domain.Enums.Chat;
using FengDeskAI.WebAPI.Authorization;
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

    /// <summary>Loại thành viên suy từ role cao nhất của người gọi (kiểm tra qua mọi role claim).</summary>
    private ParticipantType CallerParticipantType =>
        User.IsInRole(Roles.Admin) ? ParticipantType.Admin
        : User.IsInRole(Roles.Manager) ? ParticipantType.Manager
        : User.IsInRole(Roles.Staff) ? ParticipantType.Staff
        : ParticipantType.Customer;

    // ───────────── Người ↔ người ─────────────

    /// <summary>Lấy hoặc tạo phòng 1-1 với user khác.</summary>
    [HttpPost("chatbox/with/{otherUserId:guid}")]
    public async Task<IActionResult> GetOrStart(Guid otherUserId, CancellationToken ct)
        => ToActionResult(await _service.GetOrStartDirectAsync(CurrentUserId, CurrentUser.Role, otherUserId, ct));

    // ───────────── Phòng hỗ trợ (queue) ─────────────

    /// <summary>Lấy/tạo phòng hỗ trợ (mình là Owner). forceNew=true → luôn tạo phòng mới ("Trò chuyện mới").</summary>
    [HttpPost("support")]
    public async Task<IActionResult> StartSupport([FromQuery] bool forceNew, CancellationToken ct)
        => ToActionResult(await _service.GetOrStartSupportAsync(CurrentUserId, CurrentUser.Role, forceNew, ct));

    /// <summary>Hàng đợi phòng hỗ trợ đang mở (chưa có nhân sự nhận) — staff trở lên.</summary>
    [HttpGet("support/open")]
    [Authorize(Policy = AuthorizationPolicies.StaffOrAbove)]
    public async Task<IActionResult> GetOpenSupport([FromQuery] PageRequest page, CancellationToken ct)
        => ToActionResult(await _service.GetOpenSupportRoomsAsync(page, ct));

    /// <summary>Tạo phòng nhóm (mình là Owner).</summary>
    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request, CancellationToken ct)
        => ToActionResult(await _service.CreateGroupAsync(CurrentUserId, CurrentUser.Role, request, ct));

    /// <summary>Thêm thành viên: Owner, hoặc staff (tham gia/mời vào phòng hỗ trợ). Staff tự join → truyền UserId của chính mình.</summary>
    [HttpPost("chatbox/{chatboxId:guid}/participants")]
    public async Task<IActionResult> AddParticipant(Guid chatboxId, [FromBody] AddParticipantRequest request, CancellationToken ct)
        => ToActionResult(await _service.AddParticipantAsync(CurrentUserId, CallerParticipantType, chatboxId, request, ct));

    /// <summary>Xoá thành viên khỏi phòng (chỉ Owner).</summary>
    [HttpDelete("chatbox/{chatboxId:guid}/participants/{userId:guid}")]
    public async Task<IActionResult> RemoveParticipant(Guid chatboxId, Guid userId, CancellationToken ct)
        => ToActionResult(await _service.RemoveParticipantAsync(CurrentUserId, chatboxId, userId, ct));

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

    /// <summary>Đánh dấu cả phòng đã đọc (cập nhật LastReadAt của bạn).</summary>
    [HttpPatch("chatbox/{chatboxId:guid}/read-all")]
    public async Task<IActionResult> MarkChatboxAsRead(Guid chatboxId, CancellationToken ct)
        => ToActionResult(await _service.MarkChatboxAsReadAsync(CurrentUserId, chatboxId, ct));

    /// <summary>Xóa cuộc trò chuyện: rỗng → xóa hẳn; còn tin nhắn → đóng phòng (khoá, hiện mờ).</summary>
    [HttpDelete("chatbox/{chatboxId:guid}")]
    public async Task<IActionResult> DeleteChatbox(Guid chatboxId, CancellationToken ct)
        => ToActionResult(await _service.DeleteChatboxAsync(CurrentUserId, chatboxId, ct));

    /// <summary>Quyền chia sẻ thông tin của tôi cho nhân viên hỗ trợ trong phòng.</summary>
    [HttpGet("chatbox/{chatboxId:guid}/consent")]
    public async Task<IActionResult> GetConsent(Guid chatboxId, CancellationToken ct)
        => ToActionResult(await _service.GetMyConsentAsync(CurrentUserId, chatboxId, ct));

    [HttpPut("chatbox/{chatboxId:guid}/consent")]
    public async Task<IActionResult> SetConsent(Guid chatboxId, [FromBody] SetChatConsentRequest request, CancellationToken ct)
        => ToActionResult(await _service.SetMyConsentAsync(CurrentUserId, chatboxId, request, ct));

    // ───────────── Người ↔ AI ─────────────

    /// <summary>
    /// Gửi tin nhắn cho trợ lý AI. Bỏ trống ChatboxId ở lượt đầu (kèm ProductId nếu hỏi về sản phẩm) →
    /// server tạo hội thoại AI và trả lại ChatboxId để dùng cho lượt sau. Nhớ N lượt gần nhất.
    /// </summary>
    [HttpPost("ai/messages")]
    public async Task<IActionResult> SendToAi([FromBody] AiChatRequest request, CancellationToken ct)
        => ToActionResult(await _aiService.SendAsync(
            CurrentUserId, CurrentUser.Role, CurrentUser.Email, CurrentUser.Name, request, ct));

    /// <summary>
    /// Rewind: sửa (hoặc giữ nguyên để regenerate) một tin nhắn cũ của chính mình rồi gửi lại — mọi tin
    /// từ tin đó trở đi trong hội thoại bị thay thế. Chỉ áp dụng cho phòng riêng user↔AI.
    /// </summary>
    [HttpPost("ai/messages/{messageId:guid}/rewind")]
    public async Task<IActionResult> RewindAi(Guid messageId, [FromBody] AiRewindRequest request, CancellationToken ct)
        => ToActionResult(await _aiService.RewindAsync(
            CurrentUserId, CurrentUser.Role, CurrentUser.Email, CurrentUser.Name, messageId, request, ct));

    /// <summary>
    /// Lấy/tạo phòng riêng user ↔ AI và trả về ChatboxId. Trang AI lớn gọi trước khi upload ảnh
    /// (endpoint upload cần chatboxId) ở lượt đầu chưa gửi tin nào.
    /// </summary>
    [HttpPost("ai/chatbox")]
    public async Task<IActionResult> EnsureAiChatbox([FromQuery] Guid? productId, CancellationToken ct)
        => ToActionResult(await _service.EnsureAssistantAsync(CurrentUserId, CurrentUser.Role, productId, ct));

    /// <summary>Cấu hình chat AI cho FE — vd cửa sổ nhớ (số tin) để vẽ mốc "AI context limit".</summary>
    [HttpGet("ai/config")]
    public IActionResult GetAiConfig() => ToActionResult(_aiService.GetConfig());
}
