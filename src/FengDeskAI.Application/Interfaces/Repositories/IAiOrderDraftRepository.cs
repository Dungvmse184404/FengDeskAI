using FengDeskAI.Domain.Entities.CustomerCare;

namespace FengDeskAI.Application.Interfaces.Repositories;

/// <summary>
/// Draft đơn hàng của trợ lý AI. Mọi hàm GHI (trừ <see cref="AddAsync"/>) là ExecuteUpdate/ExecuteDelete:
/// chạy SQL thẳng, không qua change tracker — điều kiện để "chiếm" draft atomic và xóa CỨNG được
/// (SaveChanges của AppDbContext biến Remove() thành soft-delete).
/// </summary>
public interface IAiOrderDraftRepository
{
    /// <summary>Draft Pending còn hạn của (user, phòng) — AsNoTracking, dùng để đọc/hiển thị.</summary>
    Task<AiOrderDraft?> GetPendingAsync(Guid userId, Guid chatboxId, DateTime now, CancellationToken ct = default);

    /// <summary>Như <see cref="GetPendingAsync"/> nhưng tracked — dùng khi sửa draft rồi SaveChanges.</summary>
    Task<AiOrderDraft?> GetPendingForUpdateAsync(Guid userId, Guid chatboxId, DateTime now, CancellationToken ct = default);

    Task AddAsync(AiOrderDraft draft, CancellationToken ct = default);

    /// <summary>
    /// Chiếm draft: <c>UPDATE … SET status='Confirming' WHERE id=@id AND status='Pending'</c>.
    /// false = 0 dòng = lượt khác đã chiếm → KHÔNG được tạo đơn.
    /// </summary>
    Task<bool> TryClaimAsync(Guid draftId, DateTime now, CancellationToken ct = default);

    /// <summary>Trả draft đang Confirming về Pending (checkout lỗi) — user không mất những gì đã chọn.</summary>
    Task ReleaseClaimAsync(Guid draftId, DateTime now, CancellationToken ct = default);

    /// <summary>Trả draft về Pending kèm giá mới (giá đổi giữa prepare và confirm) để user xác nhận lại.</summary>
    Task ReleaseClaimWithNewPriceAsync(
        Guid draftId, decimal unitPrice, decimal shippingFee, decimal totalAmount, DateTime now, CancellationToken ct = default);

    /// <summary>Xóa CỨNG một draft (đặt hàng xong).</summary>
    Task<int> DeleteAsync(Guid draftId, CancellationToken ct = default);

    /// <summary>
    /// Xóa CỨNG mọi draft Pending (kể cả đã hết hạn) của (user, phòng). Dùng khi user bỏ draft và trước
    /// khi tạo draft mới (dọn bản hết hạn còn sót, nếu không unique index sẽ chặn insert).
    /// KHÔNG đụng dòng Confirming (có thể đang giữa chừng checkout).
    /// </summary>
    Task<int> DeletePendingAsync(Guid userId, Guid chatboxId, CancellationToken ct = default);

    /// <summary>Rewind hội thoại: xóa draft Pending của phòng được tạo/sửa từ mốc <paramref name="since"/> trở đi.</summary>
    Task<int> DeletePendingChangedSinceAsync(Guid chatboxId, DateTime since, CancellationToken ct = default);

    /// <summary>Worker: xóa CỨNG Pending quá expires_at + Confirming kẹt quá <paramref name="confirmingGrace"/>.</summary>
    Task<int> PurgeStaleAsync(DateTime now, TimeSpan confirmingGrace, CancellationToken ct = default);
}
