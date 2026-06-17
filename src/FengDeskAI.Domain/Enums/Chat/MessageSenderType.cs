namespace FengDeskAI.Domain.Enums.Chat;

/// <summary>Nguồn của một tin nhắn — để FE render UI đúng (logo bot, tin hệ thống...).</summary>
public enum MessageSenderType
{
    User = 0,
    AiBot = 1,
    System = 2,
}
