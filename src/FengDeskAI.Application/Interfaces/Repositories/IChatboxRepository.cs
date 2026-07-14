using FengDeskAI.Domain.Entities.Chat;
using FengDeskAI.Domain.Enums.Chat;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IChatboxRepository : IGenericRepository<Chatbox>
{
    /// <summary>Phòng kèm participants (để kiểm tra membership / hiển thị).</summary>
    Task<Chatbox?> GetWithParticipantsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Các phòng user là thành viên (kèm last message + participants), paged.</summary>
    Task<(List<Chatbox> Items, int TotalCount)> GetByUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Lấy/tạo phòng 1-1 giữa 2 user (không nhóm). Tạo participants nếu phòng mới.</summary>
    Task<Chatbox> GetOrCreateDirectAsync(Guid creatorId, ParticipantType creatorType, Guid otherUserId, CancellationToken ct = default);

    /// <summary>Lấy/tạo phòng riêng user ↔ AI (theo productId). Participants = user (Owner) + AiBot.</summary>
    Task<Chatbox> GetOrCreateAssistantAsync(Guid userId, ParticipantType userType, Guid? productId, CancellationToken ct = default);

    /// <summary>
    /// Lấy/tạo phòng hỗ trợ "mở" của customer (IsSupport, customer = Owner). Tái dùng phòng support
    /// đang mở (chưa có nhân sự, chưa ẩn) gần nhất của customer; nếu chưa có thì tạo mới.
    /// </summary>
    Task<Chatbox> GetOrCreateSupportRoomAsync(Guid userId, ParticipantType userType, CancellationToken ct = default);

    /// <summary>Luôn tạo MỘT phòng hỗ trợ mới (cho nút "Trò chuyện mới").</summary>
    Task<Chatbox> CreateSupportRoomAsync(Guid userId, ParticipantType userType, CancellationToken ct = default);

    /// <summary>Hàng đợi: các phòng support đang mở (chưa có Staff/Manager/Admin tham gia), paged. Loại phòng store-scoped (GardenStoreId != null).</summary>
    Task<(List<Chatbox> Items, int TotalCount)> GetOpenSupportRoomsAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Lấy/tạo phòng hỗ trợ "mở" của customer với MỘT SHOP cụ thể (IsSupport, GardenStoreId=storeId, customer=Owner).
    /// Tái dùng phòng đang mở (chưa có Vendor tham gia, chưa ẩn) gần nhất; nếu chưa có thì tạo mới.
    /// </summary>
    Task<Chatbox> GetOrCreateStoreSupportRoomAsync(Guid customerId, ParticipantType customerType, Guid storeId, CancellationToken ct = default);

    /// <summary>Hàng đợi: các phòng support đang mở của MỘT SHOP (chưa có Vendor tham gia), paged.</summary>
    Task<(List<Chatbox> Items, int TotalCount)> GetOpenStoreSupportRoomsAsync(Guid storeId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Các phòng của MỘT SHOP mà user (owner/staff) hiện là thành viên (đã "nhận"), paged.</summary>
    Task<(List<Chatbox> Items, int TotalCount)> GetMyStoreChatboxesAsync(Guid storeId, Guid userId, int page, int pageSize, CancellationToken ct = default);

    Task<bool> IsParticipantAsync(Guid chatboxId, Guid userId, CancellationToken ct = default);
    Task<ChatboxParticipant?> GetParticipantAsync(Guid chatboxId, Guid userId, CancellationToken ct = default);

    /// <summary>Tạo phòng nhóm: creator = Owner, các member còn lại = Member (type Customer mặc định).</summary>
    Task<Chatbox> CreateGroupAsync(Guid creatorId, ParticipantType creatorType, string? title, IEnumerable<Guid> memberUserIds, CancellationToken ct = default);

    /// <summary>Thêm 1 thành viên (bỏ qua nếu đã có).</summary>
    Task AddParticipantAsync(Guid chatboxId, Guid userId, ParticipantType type, CancellationToken ct = default);

    void RemoveParticipant(ChatboxParticipant participant);

    /// <summary>True nếu phòng có ≥1 human khác user (để phân biệt phòng chung vs riêng).</summary>
    Task<bool> HasOtherHumanAsync(Guid chatboxId, Guid userId, CancellationToken ct = default);

    /// <summary>Id các phòng "chung" (có người khác) mà user tham gia — cho ngữ cảnh multi-room.</summary>
    Task<List<Guid>> GetSharedRoomIdsAsync(Guid userId, CancellationToken ct = default);

    // ----- Consent chia sẻ thông tin cá nhân trong phòng -----

    /// <summary>Bản ghi consent của 1 người cấp trong 1 phòng (null nếu chưa cấp gì). Tracked để cập nhật.</summary>
    Task<ChatRoomDataConsent?> GetConsentAsync(Guid chatboxId, Guid granterUserId, CancellationToken ct = default);

    Task AddConsentAsync(ChatRoomDataConsent consent, CancellationToken ct = default);
}
