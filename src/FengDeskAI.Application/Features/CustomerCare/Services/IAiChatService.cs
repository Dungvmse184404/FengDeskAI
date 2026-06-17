using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.CustomerCare.DTOs;

namespace FengDeskAI.Application.Features.CustomerCare.Services;

/// <summary>
/// Trợ lý hội thoại AI có nhớ ngữ cảnh: lưu hội thoại vào chatboxes/chat_messages, nạp N lượt gần nhất
/// từ DB, có thể kèm thông tin sản phẩm (ProductId) và ảnh (base64) để hỗ trợ. Cho phép đổi model mỗi lượt.
/// </summary>
public interface IAiChatService
{
    Task<IServiceResult<AiChatResponse>> SendAsync(
        Guid userId, string? userRole, string? userEmail, string? userDisplayName,
        AiChatRequest request, CancellationToken ct = default);

    /// <summary>
    /// Bot trả lời trong 1 phòng có <c>IsAiEnabled</c> (Phase 3, do worker nền gọi). Ngữ cảnh CHỈ trong phòng đó
    /// (không gom phòng khác — tránh rò rỉ chéo). Không dùng tool (tránh nhập nhằng user trong phòng nhiều người).
    /// </summary>
    Task RespondInRoomAsync(Guid chatboxId, CancellationToken ct = default);
}
