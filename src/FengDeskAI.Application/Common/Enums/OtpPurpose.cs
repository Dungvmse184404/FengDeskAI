namespace FengDeskAI.Application.Common.Enums;

public enum OtpPurpose
{
    Register,
    ResetPassword,
    ChangeEmail,
}

public static class OtpPurposeExtensions
{
    public static string ToKey(this OtpPurpose purpose) => purpose switch
    {
        OtpPurpose.Register => "register",
        OtpPurpose.ResetPassword => "reset-password",
        OtpPurpose.ChangeEmail => "change-email",
        _ => purpose.ToString().ToLowerInvariant(),
    };
}
