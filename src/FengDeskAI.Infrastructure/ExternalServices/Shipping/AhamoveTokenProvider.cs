using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Infrastructure.ExternalServices.Shipping;

/// <summary>
/// Lấy token AhaMove qua <c>POST /v3/accounts/token</c> ({mobile, api_key}) và cache trong bộ nhớ.
/// Singleton — một token dùng chung; <see cref="RefreshAsync"/> được serialize bằng SemaphoreSlim
/// để nhiều request song song chỉ refresh một lần.
/// </summary>
public class AhamoveTokenProvider : IAhamoveTokenProvider
{
    /// <summary>Tên named-client (cấu hình BaseAddress ở DI).</summary>
    public const string HttpClientName = "AhamoveToken";

    private readonly IHttpClientFactory _httpFactory;
    private readonly AhamoveSettings _cfg;
    private readonly ILogger<AhamoveTokenProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _token;

    public AhamoveTokenProvider(IHttpClientFactory httpFactory, IOptions<AhamoveSettings> options, ILogger<AhamoveTokenProvider> logger)
    {
        _cfg = options.Value;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<string> GetAsync(CancellationToken ct = default)
        => _token ?? await RefreshAsync(ct);

    public async Task<string> RefreshAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var http = _httpFactory.CreateClient(HttpClientName);
            var res = await http.PostAsJsonAsync("/v3/accounts/token",
                new { mobile = _cfg.Mobile, api_key = _cfg.ApiKey }, ct);
            res.EnsureSuccessStatusCode();

            var dto = await res.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
            if (string.IsNullOrEmpty(dto?.Token))
                throw new InvalidOperationException("AhaMove trả về token rỗng.");

            _token = dto.Token;
            _logger.LogInformation("[Ahamove] Lấy token mới cho tài khoản {Mobile}.", _cfg.Mobile);
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken);
}
