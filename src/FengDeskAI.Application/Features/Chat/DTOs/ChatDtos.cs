namespace FengDeskAI.Application.Features.Chat.DTOs;

public class ChatboxResponse
{
    public Guid Id { get; set; }
    public Guid SenderUserId { get; set; }
    public Guid RecipientUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Tin nhắn gần nhất trong conversation.</summary>
    public ChatMessageResponse? LastMessage { get; set; }
}

public class ChatMessageResponse
{
    public Guid Id { get; set; }
    public Guid ChatboxId { get; set; }
    public Guid SenderUserId { get; set; }
    public string Content { get; set; } = null!;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SendMessageRequest
{
    public string Content { get; set; } = null!;
}

public class ChatboxListResponse
{
    public List<ChatboxResponse> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public class ChatMessageWithChatboxResponse
{
    public Guid Id { get; set; }
    public Guid ChatboxId { get; set; }
    public Guid SenderUserId { get; set; }
    public string Content { get; set; } = null!;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
