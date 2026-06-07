using System.Security.Cryptography;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Enums;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Security;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Infrastructure.Security;

public class OtpService : IOtpService
{
    private readonly IDistributedCache _cache;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateService _templates;
    private readonly OtpOptions _options;
    private readonly ILogger<OtpService> _logger;

    public OtpService(
        IDistributedCache cache,
        IEmailSender emailSender,
        IEmailTemplateService templates,
        IOptions<OtpOptions> options,
        ILogger<OtpService> logger)
    {
        _cache = cache;
        _emailSender = emailSender;
        _templates = templates;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IServiceResult> SendOtpAsync(string email, OtpPurpose purpose, CancellationToken ct = default)
    {
        var cooldownKey = CooldownKey(email, purpose);
        var existing = await _cache.GetStringAsync(cooldownKey, ct);
        if (!string.IsNullOrEmpty(existing))
        {
            return ServiceResult.Failure(
                ApiStatusCodes.BadRequest,
                $"Vui lòng đợi {_options.ResendCooldownSeconds} giây trước khi yêu cầu mã mới.");
        }

        var otp = GenerateSecureOtp(_options.Length);
        var otpKey = OtpKey(email, purpose);
        var attemptsKey = AttemptsKey(email, purpose);

        await _cache.SetStringAsync(otpKey, otp, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.TtlMinutes),
        }, ct);

        await _cache.SetStringAsync(cooldownKey, "1", new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.ResendCooldownSeconds),
        }, ct);

        await _cache.RemoveAsync(attemptsKey, ct);

        try
        {
            var (subject, html) = BuildEmailContent(email, otp, purpose);
            await _emailSender.SendAsync(new EmailMessage
            {
                To = email,
                Subject = subject,
                HtmlBody = html,
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Send OTP email failed for {Email} ({Purpose})", email, purpose);
            await _cache.RemoveAsync(otpKey, ct);
            await _cache.RemoveAsync(cooldownKey, ct);
            return ServiceResult.Failure(ApiStatusCodes.InternalServerError, "Không gửi được email. Vui lòng thử lại sau.");
        }

        return ServiceResult.Success();
    }

    public async Task<OtpVerifyResult> VerifyOtpAsync(string email, string otp, OtpPurpose purpose, CancellationToken ct = default)
    {
        var otpKey = OtpKey(email, purpose);
        var attemptsKey = AttemptsKey(email, purpose);

        var stored = await _cache.GetStringAsync(otpKey, ct);
        if (string.IsNullOrEmpty(stored))
            return OtpVerifyResult.Expired;

        var attemptsRaw = await _cache.GetStringAsync(attemptsKey, ct);
        var attempts = int.TryParse(attemptsRaw, out var n) ? n : 0;

        if (attempts >= _options.MaxVerifyAttempts)
        {
            await _cache.RemoveAsync(otpKey, ct);
            await _cache.RemoveAsync(attemptsKey, ct);
            _logger.LogWarning("Too many OTP attempts for {Email} ({Purpose})", email, purpose);
            return OtpVerifyResult.TooManyAttempts;
        }

        if (!FixedTimeEquals(stored, otp))
        {
            attempts++;
            await _cache.SetStringAsync(attemptsKey, attempts.ToString(), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.TtlMinutes),
            }, ct);
            return OtpVerifyResult.Invalid;
        }

        await _cache.RemoveAsync(otpKey, ct);
        await _cache.RemoveAsync(attemptsKey, ct);
        return OtpVerifyResult.Success;
    }

    private (string subject, string html) BuildEmailContent(string email, string otp, OtpPurpose purpose) => purpose switch
    {
        OtpPurpose.Register => ("Mã xác thực đăng ký FengDeskAI",
            _templates.BuildRegisterOtpBody(email, otp, _options.TtlMinutes)),
        OtpPurpose.ResetPassword => ("Đặt lại mật khẩu FengDeskAI",
            _templates.BuildResetPasswordOtpBody(email, otp, _options.TtlMinutes)),
        _ => ("Mã xác thực FengDeskAI",
            _templates.BuildRegisterOtpBody(email, otp, _options.TtlMinutes)),
    };

    private static string GenerateSecureOtp(int length)
    {
        var max = (int)Math.Pow(10, length);
        var bytes = RandomNumberGenerator.GetBytes(4);
        var value = BitConverter.ToUInt32(bytes, 0) % (uint)max;
        return value.ToString().PadLeft(length, '0');
    }

    private static bool FixedTimeEquals(string a, string b)
        => CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(a),
            System.Text.Encoding.UTF8.GetBytes(b));

    private static string OtpKey(string email, OtpPurpose purpose)
        => $"otp:{purpose.ToKey()}:{email}";

    private static string CooldownKey(string email, OtpPurpose purpose)
        => $"otp:{purpose.ToKey()}:cooldown:{email}";

    private static string AttemptsKey(string email, OtpPurpose purpose)
        => $"otp:{purpose.ToKey()}:attempts:{email}";
}
