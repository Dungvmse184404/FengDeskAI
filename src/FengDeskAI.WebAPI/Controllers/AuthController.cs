using FengDeskAI.Application.Features.Identity.DTOs;
using FengDeskAI.Application.Features.Identity.Services;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

[Route("api/[controller]")]
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;
    private readonly IRegistrationFlowService _registrationFlow;

    public AuthController(IAuthService authService, IRegistrationFlowService registrationFlow)
    {
        _authService = authService;
        _registrationFlow = registrationFlow;
    }

    [HttpPost("register/initiate")]
    [AllowAnonymous]
    public async Task<IActionResult> InitiateRegister([FromBody] InitiateRegisterRequest request, CancellationToken ct)
        => ToActionResult(await _registrationFlow.InitiateAsync(request, ct));

    [HttpPost("register/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyRegister([FromBody] VerifyRegisterRequest request, CancellationToken ct)
        => ToActionResult(await _registrationFlow.VerifyAsync(request, ct));

    [HttpPost("register/finalize")]
    [AllowAnonymous]
    public async Task<IActionResult> FinalizeRegister([FromBody] FinalizeRegisterRequest request, CancellationToken ct)
        => ToActionResult(await _registrationFlow.FinalizeAsync(request, ct));

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
        => ToActionResult(await _authService.LoginAsync(request, ct));

    /// <summary>Đăng nhập/đăng ký bằng Google — nhận ID token (credential) từ Google Identity Services ở FE.</summary>
    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginWithGoogle([FromBody] GoogleLoginRequest request, CancellationToken ct)
        => ToActionResult(await _authService.LoginWithGoogleAsync(request, ct));

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
        => ToActionResult(await _authService.RefreshAsync(request, ct));

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken ct)
        => ToActionResult(await _authService.LogoutAsync(request.RefreshToken, ct));

    /// <summary>Thông tin user đang đăng nhập (test JWT auth có hoạt động).</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
        => ToActionResult(await _authService.GetMeAsync(CurrentUserId, ct));

    /// <summary>Cập nhật giờ sinh (HH:mm, null để xóa) — cần cho Tứ Trụ/Bát Tự đầy đủ.</summary>
    [HttpPut("me/birth-time")]
    [Authorize]
    public async Task<IActionResult> UpdateBirthTime([FromBody] UpdateBirthTimeRequest request, CancellationToken ct)
    {
        TimeOnly? time = null;
        if (!string.IsNullOrWhiteSpace(request.BirthTime))
        {
            if (!TimeOnly.TryParse(request.BirthTime, out var parsed))
                return BadRequest(new { message = "Giờ sinh phải theo định dạng HH:mm." });
            time = parsed;
        }
        return ToActionResult(await _authService.UpdateBirthTimeAsync(CurrentUserId, time, ct));
    }

    /// <summary>Cập nhật họ tên / SĐT / giới tính / ngày sinh của chính mình.</summary>
    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
        => ToActionResult(await _authService.UpdateProfileAsync(CurrentUserId, request, ct));

    // ===== Đổi email: 4 bước tuần tự, mỗi bước sau cần kết quả của bước trước =====

    /// <summary>B1 — gửi OTP tới email hiện tại.</summary>
    [HttpPost("me/email/initiate")]
    [Authorize]
    public async Task<IActionResult> InitiateEmailChange(CancellationToken ct)
        => ToActionResult(await _authService.InitiateEmailChangeAsync(CurrentUserId, ct));

    /// <summary>B2 — xác thực OTP email hiện tại, nhận changeEmailToken.</summary>
    [HttpPost("me/email/verify-current")]
    [Authorize]
    public async Task<IActionResult> VerifyCurrentEmail([FromBody] VerifyCurrentEmailRequest request, CancellationToken ct)
        => ToActionResult(await _authService.VerifyCurrentEmailAsync(CurrentUserId, request, ct));

    /// <summary>B3 — khai email mới, gửi OTP tới hòm thư đó.</summary>
    [HttpPost("me/email/request-new")]
    [Authorize]
    public async Task<IActionResult> RequestNewEmail([FromBody] RequestNewEmailRequest request, CancellationToken ct)
        => ToActionResult(await _authService.RequestNewEmailAsync(CurrentUserId, request, ct));

    /// <summary>B4 — xác thực OTP email mới, áp dụng đổi email và cấp lại token.</summary>
    [HttpPost("me/email/confirm")]
    [Authorize]
    public async Task<IActionResult> ConfirmNewEmail([FromBody] ConfirmNewEmailRequest request, CancellationToken ct)
        => ToActionResult(await _authService.ConfirmNewEmailAsync(CurrentUserId, request, ct));
}
