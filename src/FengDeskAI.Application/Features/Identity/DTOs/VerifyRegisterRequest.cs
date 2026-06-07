namespace FengDeskAI.Application.Features.Identity.DTOs;

public class VerifyRegisterRequest
{
    public string Email { get; set; } = null!;
    public string Otp { get; set; } = null!;
}
