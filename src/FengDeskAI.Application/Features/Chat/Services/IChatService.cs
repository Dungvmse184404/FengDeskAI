using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Chat.DTOs;
using FengDeskAI.Domain.Enums.Chat;

namespace FengDeskAI.Application.Features.Chat.Services;

public interface IChatService
{
    /// <summary>Lấy hoặc tạo phòng 1-1 với user khác (không cần biết trước — chỉ truyền otherUserId).</summary>
    Task<IServiceResult<ChatboxResponse>> GetOrStartDirectAsync(Guid userId, string? userRole, Guid otherUserId, CancellationToken ct = default);

    /// <summary>
    /// Lấy/tạo phòng riêng user ↔ trợ lý AI và trả về (kèm ChatboxId). Dùng ở trang AI lớn để có
    /// chatbox TRƯỚC khi upload ảnh (endpoint upload cần chatboxId + là participant) — lượt đầu chưa gửi tin.
    /// </summary>
    Task<IServiceResult<ChatboxResponse>> EnsureAssistantAsync(Guid userId, string? userRole, Guid? productId, CancellationToken ct = default);

    /// <summary>
    /// Lấy/tạo phòng hỗ trợ của customer (Customer = Owner). <paramref name="forceNew"/> = true → luôn tạo
    /// phòng mới (nút "Trò chuyện mới"); false → tái dùng phòng đang mở nếu có (auto khi chưa có phòng).
    /// </summary>
    Task<IServiceResult<ChatboxResponse>> GetOrStartSupportAsync(Guid userId, string? userRole, bool forceNew, CancellationToken ct = default);

    /// <summary>
    /// Khách "xóa" phòng: KHÔNG có tin nhắn → xóa hẳn; CÓ tin nhắn → đóng phòng (khoá, hiện mờ, không gửi
    /// tin mới, rời khỏi hàng đợi hỗ trợ). Chỉ chủ phòng được thao tác.
    /// </summary>
    Task<IServiceResult> DeleteChatboxAsync(Guid userId, Guid chatboxId, CancellationToken ct = default);

    /// <summary>Đọc quyền chia sẻ thông tin của tôi trong phòng (mặc định tất cả false).</summary>
    Task<IServiceResult<ChatConsentResponse>> GetMyConsentAsync(Guid userId, Guid chatboxId, CancellationToken ct = default);

    /// <summary>Cập nhật quyền chia sẻ thông tin của tôi trong phòng.</summary>
    Task<IServiceResult<ChatConsentResponse>> SetMyConsentAsync(Guid userId, Guid chatboxId, SetChatConsentRequest request, CancellationToken ct = default);

    /// <summary>Hàng đợi phòng hỗ trợ đang mở (chưa có nhân sự hỗ trợ) — cho staff trở lên.</summary>
    Task<IServiceResult<ChatboxListResponse>> GetOpenSupportRoomsAsync(PageRequest page, CancellationToken ct = default);

    /// <summary>Tạo phòng nhóm (creator = Owner). MemberUserIds tuỳ chọn.</summary>
    Task<IServiceResult<ChatboxResponse>> CreateGroupAsync(Guid userId, string? userRole, CreateGroupRequest request, CancellationToken ct = default);

    /// <summary>
    /// Thêm thành viên vào phòng. Cho phép: Owner; staff thành viên của phòng; hoặc staff (theo
    /// <paramref name="callerType"/>) tham gia/ mời vào phòng hỗ trợ (IsSupport). callerType suy từ JWT role.
    /// </summary>
    Task<IServiceResult> AddParticipantAsync(Guid callerId, ParticipantType callerType, Guid chatboxId, AddParticipantRequest request, CancellationToken ct = default);

    /// <summary>Xoá thành viên khỏi phòng (chỉ Owner; không tự xoá Owner cuối).</summary>
    Task<IServiceResult> RemoveParticipantAsync(Guid userId, Guid chatboxId, Guid targetUserId, CancellationToken ct = default);

    /// <summary>Danh sách phòng của user hiện tại (paged).</summary>
    Task<IServiceResult<ChatboxListResponse>> GetMineAsync(Guid userId, PageRequest page, CancellationToken ct = default);

    /// <summary>Messages trong phòng (paged, mới nhất trước).</summary>
    Task<IServiceResult<PagedResult<ChatMessageResponse>>> GetMessagesAsync(
        Guid userId, Guid chatboxId, PageRequest page, CancellationToken ct = default);

    /// <summary>Gửi message (text và/hoặc link ảnh). role/email suy ra SenderName.</summary>
    Task<IServiceResult<ChatMessageResponse>> SendMessageAsync(
        Guid userId, string? userRole, string? userEmail, Guid chatboxId, SendMessageRequest request, CancellationToken ct = default);

    /// <summary>Tải ảnh chat lên storage (Chat_images/{chatboxId}/...) và trả link.</summary>
    Task<IServiceResult<string>> UploadImageAsync(
        Guid userId, Guid chatboxId, Stream content, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>Đánh dấu cả phòng đã đọc (cập nhật LastReadAt của participant).</summary>
    Task<IServiceResult> MarkChatboxAsReadAsync(Guid userId, Guid chatboxId, CancellationToken ct = default);

    /// <summary>Kiểm tra user có là thành viên phòng không.</summary>
    Task<IServiceResult> ValidateChatboxAccessAsync(Guid userId, Guid chatboxId, CancellationToken ct = default);

    void RecordUserConnection(Guid userId, string connectionId);
    void RemoveUserConnection(Guid userId, string connectionId);
}
