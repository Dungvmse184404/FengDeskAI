namespace FengDeskAI.Infrastructure.Authentication;

public class GoogleAuthSettings
{
    public const string SectionName = "GoogleAuth";

    /// <summary>OAuth 2.0 Client ID (Web application) tạo ở Google Cloud Console → dùng làm "audience" khi verify ID token.</summary>
    public string ClientId { get; set; } = null!;
}
