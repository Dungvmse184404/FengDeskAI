namespace FengDeskAI.Application.Features.Identity.DTOs;

public class GoogleLoginRequest
{
    /// <summary>ID token (JWT "credential") trả về từ Google Identity Services ở FE.</summary>
    public string IdToken { get; set; } = null!;
}
