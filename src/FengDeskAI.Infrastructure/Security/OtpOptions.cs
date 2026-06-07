namespace FengDeskAI.Infrastructure.Security;

public class OtpOptions
{
    public const string SectionName = "Otp";

    public int Length { get; set; } = 6;
    public int TtlMinutes { get; set; } = 10;
    public int ResendCooldownSeconds { get; set; } = 60;
    public int MaxVerifyAttempts { get; set; } = 5;
}
