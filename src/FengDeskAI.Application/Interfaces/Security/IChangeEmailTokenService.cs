namespace FengDeskAI.Application.Interfaces.Security;

/// <summary>
/// Trạng thái của một phiên đổi email đang dở.
/// <paramref name="PendingNewEmail"/> null = mới xác thực xong email cũ, chưa khai email mới.
/// </summary>
public sealed record ChangeEmailSession(Guid UserId, string? PendingNewEmail);

/// <summary>
/// Token ngắn hạn buộc 3 bước cuối của luồng đổi email (xác thực mail cũ → khai mail mới →
/// xác thực mail mới) vào cùng một phiên, để không ai nhảy cóc qua bước xác thực mail cũ.
/// </summary>
public interface IChangeEmailTokenService
{
    Task<string> IssueAsync(ChangeEmailSession session, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Đọc phiên mà KHÔNG xóa — dùng ở các bước trung gian. Null nếu token sai/hết hạn.</summary>
    Task<ChangeEmailSession?> PeekAsync(string token, CancellationToken ct = default);

    /// <summary>Ghi đè phiên (giữ nguyên token) khi user khai email mới. TTL được đặt lại.</summary>
    Task UpdateAsync(string token, ChangeEmailSession session, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Hủy phiên — gọi sau khi đổi email thành công để token không dùng lại được.</summary>
    Task RevokeAsync(string token, CancellationToken ct = default);
}
