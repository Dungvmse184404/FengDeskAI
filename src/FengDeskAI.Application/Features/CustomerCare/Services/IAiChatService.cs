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
    /// Bot trả lời khi có người gọi <c>@AI</c> trong phòng nhiều người (do worker nền gọi).
    /// Ngữ cảnh = lịch sử phòng hiện tại + (chỉ) các tin của CHÍNH người gọi ở những phòng public khác —
    /// tuyệt đối không nạp nội dung phòng private. Tool chạy theo scope của <paramref name="triggeredByUserId"/>.
    /// </summary>
    Task RespondInRoomAsync(Guid chatboxId, Guid triggeredByUserId, CancellationToken ct = default);

    /// <summary>
    /// Rewind (sửa & gửi lại) một tin nhắn cũ của chính user trong phòng riêng user↔AI: soft-delete tin đó
    /// và mọi tin sau nó trong chatbox, rồi gửi lại nội dung (mới hoặc giữ nguyên) qua <see cref="SendAsync"/>.
    /// </summary>
    Task<IServiceResult<AiChatResponse>> RewindAsync(
        Guid userId, string? userRole, string? userEmail, string? userDisplayName,
        Guid messageId, AiRewindRequest request, CancellationToken ct = default);

    /// <summary>Cấu hình chat AI mà FE cần biết — vd cửa sổ nhớ để vẽ mốc "AI context limit" trong khung chat.</summary>
    IServiceResult<AiChatConfigResponse> GetConfig();
}
