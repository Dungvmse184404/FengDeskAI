using System.Security.Cryptography;
using FengDeskAI.Application.Interfaces.Security;
using Microsoft.Extensions.Caching.Distributed;

namespace FengDeskAI.Infrastructure.Security;

/// <summary>
/// Lưu phiên đổi email trong distributed cache dưới dạng "{userId}|{newEmail?}" — cùng cách
/// <see cref="RegistrationTokenService"/> lưu phiên đăng ký, không cần thêm bảng DB.
/// </summary>
public class ChangeEmailTokenService : IChangeEmailTokenService
{
    private readonly IDistributedCache _cache;

    public ChangeEmailTokenService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<string> IssueAsync(ChangeEmailSession session, TimeSpan ttl, CancellationToken ct = default)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        await WriteAsync(token, session, ttl, ct);
        return token;
    }

    public async Task<ChangeEmailSession?> PeekAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var raw = await _cache.GetStringAsync(Key(token), ct);
        if (string.IsNullOrEmpty(raw)) return null;

        var separator = raw.IndexOf('|');
        if (separator < 0 || !Guid.TryParse(raw[..separator], out var userId)) return null;

        var pending = raw[(separator + 1)..];
        return new ChangeEmailSession(userId, string.IsNullOrEmpty(pending) ? null : pending);
    }

    public Task UpdateAsync(string token, ChangeEmailSession session, TimeSpan ttl, CancellationToken ct = default)
        => WriteAsync(token, session, ttl, ct);

    public Task RevokeAsync(string token, CancellationToken ct = default)
        => string.IsNullOrWhiteSpace(token) ? Task.CompletedTask : _cache.RemoveAsync(Key(token), ct);

    private Task WriteAsync(string token, ChangeEmailSession session, TimeSpan ttl, CancellationToken ct)
        => _cache.SetStringAsync(
            Key(token),
            $"{session.UserId}|{session.PendingNewEmail}",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            ct);

    private static string Key(string token) => $"change-email:token:{token}";
}
