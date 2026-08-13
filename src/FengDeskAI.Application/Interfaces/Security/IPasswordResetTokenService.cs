namespace FengDeskAI.Application.Interfaces.Security;

/// <summary>
/// Token ngắn hạn cấp sau khi user xác thực đúng OTP quên mật khẩu — buộc bước đặt lại mật khẩu
/// phải đi qua bước xác thực OTP, để không ai đổi mật khẩu chỉ bằng email.
/// Lưu userId (không phải email) nên token vẫn đúng chủ kể cả khi email đổi giữa chừng.
/// </summary>
public interface IPasswordResetTokenService
{
    Task<string> IssueAsync(Guid userId, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Đọc và XÓA token — dùng 1 lần. Null nếu token sai/hết hạn/đã dùng.</summary>
    Task<Guid?> ConsumeAsync(string token, CancellationToken ct = default);
}
