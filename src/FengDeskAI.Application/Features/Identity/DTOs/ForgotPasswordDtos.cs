namespace FengDeskAI.Application.Features.Identity.DTOs;

/// <summary>Bước 1 — khai email của tài khoản cần lấy lại mật khẩu, hệ thống gửi OTP tới hòm thư đó.</summary>
public class ForgotPasswordRequest
{
    public string Email { get; set; } = null!;
}

/// <summary>Bước 2 — xác thực OTP vừa nhận, đổi lấy token cho bước đặt mật khẩu mới.</summary>
public class VerifyForgotPasswordOtpRequest
{
    public string Email { get; set; } = null!;
    public string Otp { get; set; } = null!;
}

/// <summary>Kết quả bước 2 — token dùng 1 lần, mở khóa bước đặt lại mật khẩu.</summary>
public class ResetPasswordTokenResponse
{
    public string ResetPasswordToken { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}

/// <summary>Bước 3 — đặt mật khẩu mới bằng token của bước 2.</summary>
public class ResetPasswordRequest
{
    public string ResetPasswordToken { get; set; } = null!;
    public string NewPassword { get; set; } = null!;
}
