using System.Security.Cryptography;
using FengDeskAI.Application.Interfaces.Security;
using Microsoft.Extensions.Caching.Distributed;

namespace FengDeskAI.Infrastructure.Security;

/// <summary>
/// Lưu phiên đặt lại mật khẩu trong distributed cache — cùng cách
/// <see cref="RegistrationTokenService"/> lưu phiên đăng ký, không cần thêm bảng DB.
/// </summary>
public class PasswordResetTokenService : IPasswordResetTokenService
{
    private readonly IDistributedCache _cache;

    public PasswordResetTokenService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<string> IssueAsync(Guid userId, TimeSpan ttl, CancellationToken ct = default)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        await _cache.SetStringAsync(Key(token), userId.ToString(), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
        }, ct);

        return token;
    }

    public async Task<Guid?> ConsumeAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var key = Key(token);
        var raw = await _cache.GetStringAsync(key, ct);
        if (string.IsNullOrEmpty(raw) || !Guid.TryParse(raw, out var userId)) return null;

        await _cache.RemoveAsync(key, ct);
        return userId;
    }

    private static string Key(string token) => $"reset-password:token:{token}";
}
