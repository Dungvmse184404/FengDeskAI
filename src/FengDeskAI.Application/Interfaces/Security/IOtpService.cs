using FengDeskAI.Application.Common.Enums;
using FengDeskAI.Application.Common.Results;

namespace FengDeskAI.Application.Interfaces.Security;

public interface IOtpService
{
    Task<IServiceResult> SendOtpAsync(string email, OtpPurpose purpose, CancellationToken ct = default);
    Task<OtpVerifyResult> VerifyOtpAsync(string email, string otp, OtpPurpose purpose, CancellationToken ct = default);
}
