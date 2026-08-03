namespace FengDeskAI.Application.Interfaces.External;

public interface IEmailTemplateService
{
    string BuildRegisterOtpBody(string email, string otp, int expiryMinutes);
    string BuildResetPasswordOtpBody(string email, string otp, int expiryMinutes);

    /// <summary>Mã xác thực cho luồng đổi email — gửi tới cả hòm thư cũ (xác nhận chủ sở hữu) lẫn hòm thư mới.</summary>
    string BuildChangeEmailOtpBody(string email, string otp, int expiryMinutes);
}
