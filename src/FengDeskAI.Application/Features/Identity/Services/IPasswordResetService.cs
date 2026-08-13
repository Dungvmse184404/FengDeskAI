using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Identity.DTOs;

namespace FengDeskAI.Application.Features.Identity.Services;

/// <summary>
/// Quên mật khẩu — 3 bước, mỗi bước phải qua bước trước (ràng bằng resetPasswordToken).
/// Cùng khuôn với luồng đăng ký (<see cref="IRegistrationFlowService"/>).
/// </summary>
public interface IPasswordResetService
{
    /// <summary>B1: kiểm tra email có tài khoản → gửi OTP tới hòm thư đó.</summary>
    Task<IServiceResult> InitiateAsync(ForgotPasswordRequest request, CancellationToken ct = default);

    /// <summary>B2: xác thực OTP → cấp resetPasswordToken dùng 1 lần.</summary>
    Task<IServiceResult<ResetPasswordTokenResponse>> VerifyOtpAsync(
        VerifyForgotPasswordOtpRequest request, CancellationToken ct = default);

    /// <summary>
    /// B3: đặt mật khẩu mới bằng token của B2, đồng thời thu hồi toàn bộ phiên đăng nhập cũ
    /// (refresh token + access token qua <c>TokenVersion</c>).
    /// </summary>
    Task<IServiceResult> ResetAsync(ResetPasswordRequest request, CancellationToken ct = default);
}
