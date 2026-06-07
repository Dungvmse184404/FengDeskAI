using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Identity.DTOs;
using FengDeskAI.Application.Features.Identity.Services;
using FengDeskAI.Application.Interfaces.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IRegistrationFlowService _registrationFlow;
    private readonly ICurrentUserService _currentUser;

    public AuthController(
        IAuthService authService,
        IRegistrationFlowService registrationFlow,
        ICurrentUserService currentUser)
    {
        _authService = authService;
        _registrationFlow = registrationFlow;
        _currentUser = currentUser;
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

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
        => ToActionResult(await _authService.RefreshAsync(request, ct));

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken ct)
        => ToActionResult(await _authService.LogoutAsync(request.RefreshToken, ct));

    /// <summary>
    /// Trả về thông tin user đang đăng nhập (test JWT auth có hoạt động).
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
            return StatusCode(ApiStatusCodes.Unauthorized,
                ServiceResult.Failure(ApiStatusCodes.Unauthorized, "Token không hợp lệ."));

        return ToActionResult(await _authService.GetMeAsync(userId.Value, ct));
    }

    private IActionResult ToActionResult(IServiceResult result)
        => StatusCode(result.StatusCode, result);

    private IActionResult ToActionResult<T>(IServiceResult<T> result)
        => StatusCode(result.StatusCode, result);
}
