using FengDeskAI.Domain.Enums;

namespace FengDeskAI.Application.Features.Identity.DTOs;

public class FinalizeRegisterRequest
{
    public string RegistrationToken { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public Gender Gender { get; set; } = Gender.Unspecified;
}
