namespace FengDeskAI.Infrastructure.ExternalServices.Mail;

public class MailSettings
{
    public const string SectionName = "MailSettings";

    public string Host { get; set; } = null!;
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string FromEmail { get; set; } = null!;
    public string FromName { get; set; } = "FengDeskAI";
}
