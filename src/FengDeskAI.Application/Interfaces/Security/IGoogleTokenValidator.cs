namespace FengDeskAI.Application.Interfaces.Security;

/// <summary>Kết quả verify Google ID token — trích từ payload đã được xác thực chữ ký + audience.</summary>
public record GoogleUserInfo(string GoogleId, string Email, bool EmailVerified, string? FullName, string? PictureUrl);

public interface IGoogleTokenValidator
{
    /// <summary>
    /// Verify chữ ký + audience (ClientId) + issuer của Google ID token (JWT credential từ Google Identity Services).
    /// Trả về null nếu token không hợp lệ/hết hạn/audience sai.
    /// </summary>
    Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken ct = default);
}
