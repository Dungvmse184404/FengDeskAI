using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Identity.DTOs;

namespace FengDeskAI.Application.Features.Identity.Services;

public interface IRegistrationFlowService
{
    Task<IServiceResult> InitiateAsync(InitiateRegisterRequest request, CancellationToken ct = default);
    Task<IServiceResult<VerifyRegisterResponse>> VerifyAsync(VerifyRegisterRequest request, CancellationToken ct = default);
    Task<IServiceResult<AuthResponse>> FinalizeAsync(FinalizeRegisterRequest request, CancellationToken ct = default);
}
