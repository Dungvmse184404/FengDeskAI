namespace FengDeskAI.Application.Features.CustomerCare.DTOs;

/// <summary>
/// Draft đơn hàng do <c>prepare_order</c> sinh — lưu tạm trong <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/>
/// (TTL 15'), KHÔNG ghi DB. <c>confirm_order</c> đọc lại bằng draftId để re-validate rồi mới tạo đơn thật.
/// v1 chỉ hỗ trợ 1 sản phẩm/variant mỗi draft.
/// </summary>
public sealed record OrderDraft(
    Guid UserId,
    Guid ProductItemId,
    int Quantity,
    decimal UnitPriceSnapshot,
    Guid ShippingAddressId,
    DateTime CreatedAt);

/// <summary>Key cache dùng chung giữa prepare_order và confirm_order — scope theo user để tránh đụng draft chéo user.</summary>
public static class OrderDraftCacheKey
{
    public static string For(Guid userId, Guid draftId) => $"order-draft:{userId}:{draftId}";
}
