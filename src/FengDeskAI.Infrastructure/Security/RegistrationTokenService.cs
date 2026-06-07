using System.Security.Cryptography;
using FengDeskAI.Application.Interfaces.Security;
using Microsoft.Extensions.Caching.Distributed;

namespace FengDeskAI.Infrastructure.Security;

public class RegistrationTokenService : IRegistrationTokenService
{
    private readonly IDistributedCache _cache;

    public RegistrationTokenService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<string> IssueAsync(string email, TimeSpan ttl, CancellationToken ct = default)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        await _cache.SetStringAsync(Key(token), email, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
        }, ct);

        return token;
    }

    public async Task<string?> ConsumeAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var key = Key(token);
        var email = await _cache.GetStringAsync(key, ct);
        if (string.IsNullOrEmpty(email)) return null;

        await _cache.RemoveAsync(key, ct);
        return email;
    }

    private static string Key(string token) => $"register:token:{token}";
}
