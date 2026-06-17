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
}
