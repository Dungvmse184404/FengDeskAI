using FengDeskAI.Application.Interfaces.Security;
using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Infrastructure.Authentication;

/// <summary>
/// Verify Google ID token (JWT "credential" trả về từ Google Identity Services ở FE) offline —
/// kiểm tra chữ ký (JWKS của Google), issuer, expiry và audience (phải khớp GoogleAuth:ClientId).
/// </summary>
public class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly GoogleAuthSettings _settings;
    private readonly ILogger<GoogleTokenValidator> _logger;

    public GoogleTokenValidator(IOptions<GoogleAuthSettings> options, ILogger<GoogleTokenValidator> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            return null;

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _settings.ClientId },
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            return new GoogleUserInfo(
                GoogleId: payload.Subject,
                Email: payload.Email.Trim().ToLowerInvariant(),
                EmailVerified: payload.EmailVerified,
                FullName: string.IsNullOrWhiteSpace(payload.Name) ? null : payload.Name,
                PictureUrl: payload.Picture);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Google ID token không hợp lệ.");
            return null;
        }
    }
}
