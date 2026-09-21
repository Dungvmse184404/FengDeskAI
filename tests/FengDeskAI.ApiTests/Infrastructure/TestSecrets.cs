using System.Security.Cryptography;

namespace FengDeskAI.ApiTests.Infrastructure;

/// <summary>
/// Sinh giá trị ngẫu nhiên cho mỗi lần chạy test (JWT secret, mật khẩu user mẫu, webhook secret…).
///
/// Lý do không hardcode: giá trị cố định trong mã nguồn trông như secret thật, dễ bị copy sang môi
/// trường khác, và làm mã nguồn bị công cụ quét secret báo động. Ở đây app tự ký và tự verify trong
/// cùng một tiến trình nên giá trị chỉ cần hợp lệ, không cần cố định giữa các lần chạy.
/// </summary>
public static class TestSecrets
{
    /// <summary>Chuỗi base64url ngẫu nhiên. Mặc định 32 byte — đủ dài cho HMAC-SHA256 của JwtSettings.</summary>
    public static string NewSecret(int byteLength = 32)
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    /// <summary>
    /// Mật khẩu ngẫu nhiên cho user mẫu. Ghép thêm ký tự hoa/thường/số/đặc biệt để vẫn hợp lệ nếu
    /// sau này <c>PasswordPolicy</c> siết thêm ràng buộc ngoài độ dài.
    /// </summary>
    public static string NewPassword() => $"Aa1@{NewSecret(16)}";
}
