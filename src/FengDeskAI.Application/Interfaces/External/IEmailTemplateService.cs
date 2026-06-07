namespace FengDeskAI.Application.Interfaces.External;

public interface IEmailTemplateService
{
    string BuildRegisterOtpBody(string email, string otp, int expiryMinutes);
    string BuildResetPasswordOtpBody(string email, string otp, int expiryMinutes);
}
