namespace FengDeskAI.Application.Common.Constants;

/// <summary>
/// Ràng buộc mật khẩu dùng chung cho MỌI luồng đặt/đổi mật khẩu (đăng ký, quên mật khẩu…).
/// Sửa ở đây là mọi luồng đổi theo — đừng validate rời rạc trong từng service.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 6;

    /// <summary>Trả message lỗi, hoặc null nếu mật khẩu hợp lệ.</summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return ApiStatusMessages.Password.Required;

        if (password.Length < MinLength)
            return string.Format(ApiStatusMessages.Password.TooShortFormat, MinLength);

        return null;
    }
}
