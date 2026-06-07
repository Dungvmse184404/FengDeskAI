using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.Infrastructure.ExternalServices.Mail;

public class EmailTemplateService : IEmailTemplateService
{
    public string BuildRegisterOtpBody(string email, string otp, int expiryMinutes)
        => BuildOtpBody(
            title: "Xác thực đăng ký FengDeskAI",
            intro: $"Bạn đang đăng ký tài khoản FengDeskAI với email <b>{email}</b>.",
            otp: otp,
            expiryMinutes: expiryMinutes);

    public string BuildResetPasswordOtpBody(string email, string otp, int expiryMinutes)
        => BuildOtpBody(
            title: "Đặt lại mật khẩu FengDeskAI",
            intro: $"Bạn vừa yêu cầu đặt lại mật khẩu cho email <b>{email}</b>.",
            otp: otp,
            expiryMinutes: expiryMinutes);

    private static string BuildOtpBody(string title, string intro, string otp, int expiryMinutes) => $"""
        <!DOCTYPE html>
        <html>
        <body style="font-family: Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 24px; color: #1f2937;">
          <h2 style="color: #0f766e;">{title}</h2>
          <p>{intro}</p>
          <p>Mã xác thực của bạn là:</p>
          <div style="font-size: 32px; font-weight: bold; letter-spacing: 6px; background: #f0fdfa; color: #0f766e; padding: 16px 24px; text-align: center; border-radius: 8px; margin: 16px 0;">
            {otp}
          </div>
          <p>Mã có hiệu lực trong <b>{expiryMinutes} phút</b>. Vui lòng không chia sẻ mã này với bất kỳ ai.</p>
          <p style="color: #6b7280; font-size: 13px;">Nếu bạn không thực hiện yêu cầu này, hãy bỏ qua email.</p>
          <hr style="border: none; border-top: 1px solid #e5e7eb; margin: 24px 0;" />
          <p style="color: #9ca3af; font-size: 12px;">© FengDeskAI</p>
        </body>
        </html>
        """;
}
