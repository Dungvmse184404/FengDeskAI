using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Enums;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Identity.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Application.Interfaces.Security;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Application.Features.Identity.Services;

public class PasswordResetService : IPasswordResetService
{
    /// <summary>Đủ để user nghĩ và gõ mật khẩu mới, không treo phiên quá lâu.</summary>
    private static readonly TimeSpan ResetTokenTtl = TimeSpan.FromMinutes(15);

    private readonly IUnitOfWork _uow;
    private readonly IOtpService _otpService;
    private readonly IPasswordResetTokenService _resetTokens;
    private readonly IPasswordService _passwordService;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        IUnitOfWork uow,
        IOtpService otpService,
        IPasswordResetTokenService resetTokens,
        IPasswordService passwordService,
        ILogger<PasswordResetService> logger)
    {
        _uow = uow;
        _otpService = otpService;
        _resetTokens = resetTokens;
        _passwordService = passwordService;
        _logger = logger;
    }

    public async Task<IServiceResult> InitiateAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);
        if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.PasswordReset.EmailInvalid);

        var user = await _uow.Users.GetByEmailAsync(email, ct);
        if (user is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.PasswordReset.EmailNotFound);

        if (!user.IsActive)
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Auth.AccountDisabled);

        // Tài khoản Google chưa có mật khẩu vẫn đi được luồng này — OTP về đúng hòm thư đó nên
        // an toàn, và user có thêm cách đăng nhập bằng mật khẩu.
        var sendResult = await _otpService.SendOtpAsync(email, OtpPurpose.ResetPassword, ct);
        if (!sendResult.IsSuccess) return sendResult;

        return ServiceResult.Success(ApiStatusMessages.PasswordReset.OtpSent);
    }

    public async Task<IServiceResult<ResetPasswordTokenResponse>> VerifyOtpAsync(
        VerifyForgotPasswordOtpRequest request, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);

        var user = await _uow.Users.GetByEmailAsync(email, ct);
        if (user is null)
            return ServiceResult<ResetPasswordTokenResponse>.Failure(
                ApiStatusCodes.NotFound, ApiStatusMessages.PasswordReset.EmailNotFound);

        if (!user.IsActive)
            return ServiceResult<ResetPasswordTokenResponse>.Failure(
                ApiStatusCodes.Forbidden, ApiStatusMessages.Auth.AccountDisabled);

        var otpFailure = MapOtpFailure(
            await _otpService.VerifyOtpAsync(email, request.Otp, OtpPurpose.ResetPassword, ct));
        if (otpFailure is not null)
            return ServiceResult<ResetPasswordTokenResponse>.Failure(ApiStatusCodes.BadRequest, otpFailure);

        var token = await _resetTokens.IssueAsync(user.Id, ResetTokenTtl, ct);
        var response = new ResetPasswordTokenResponse
        {
            ResetPasswordToken = token,
            ExpiresAt = DateTime.UtcNow.Add(ResetTokenTtl),
        };

        return ServiceResult<ResetPasswordTokenResponse>.Success(response, ApiStatusMessages.PasswordReset.VerifySuccess);
    }

    public async Task<IServiceResult> ResetAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var password = request.NewPassword;
        if (PasswordPolicy.Validate(password) is { } passwordError)
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, passwordError);

        // Token dùng 1 lần: đọc là mất. Mọi kiểm tra rẻ tiền ở trên phải chạy TRƯỚC để user gõ
        // mật khẩu sai định dạng không bị đá về bước xác thực OTP.
        var userId = await _resetTokens.ConsumeAsync(request.ResetPasswordToken, ct);
        if (userId is null)
            return ServiceResult.Failure(ApiStatusCodes.Unauthorized, ApiStatusMessages.PasswordReset.SessionInvalid);

        var user = await _uow.Users.GetByIdAsync(userId.Value, ct);
        if (user is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Auth.UserNotFound);

        if (!user.IsActive)
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Auth.AccountDisabled);

        // PasswordHash null = tài khoản Google chưa từng đặt mật khẩu → không có gì để so.
        if (user.PasswordHash is not null && _passwordService.Verify(password, user.PasswordHash))
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.PasswordReset.PasswordSameAsOld);

        user.PasswordHash = _passwordService.Hash(password);

        // Đá mọi phiên cũ: refresh token bị thu hồi, access token còn hạn chết theo token_version.
        await _uow.RefreshTokens.RevokeAllActiveForUserAsync(user.Id, ct);
        user.TokenVersion++;

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} reset password; all sessions revoked", user.Id);

        return ServiceResult.Success(ApiStatusMessages.PasswordReset.Success);
    }

    /// <summary>Đổi kết quả verify OTP thành message lỗi; null = hợp lệ.</summary>
    private static string? MapOtpFailure(OtpVerifyResult result) => result switch
    {
        OtpVerifyResult.Invalid => ApiStatusMessages.Registration.OtpIncorrect,
        OtpVerifyResult.Expired => ApiStatusMessages.Registration.OtpExpired,
        OtpVerifyResult.TooManyAttempts => ApiStatusMessages.Registration.OtpTooManyAttempts,
        _ => null,
    };

    private static string Normalize(string? email) => email?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool IsValidEmail(string email)
    {
        try
        {
            return new System.Net.Mail.MailAddress(email).Address == email;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
