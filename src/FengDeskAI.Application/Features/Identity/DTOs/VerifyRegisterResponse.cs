namespace FengDeskAI.Application.Features.Identity.DTOs;

public class VerifyRegisterResponse
{
    public string RegistrationToken { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}
