using FengDeskAI.Domain.Enums.Chat;

namespace FengDeskAI.Application.Features.Chat.DTOs;

public class ChatboxResponse
{
    public Guid Id { get; set; }
    public ChatboxType Type { get; set; }
    public Guid SenderUserId { get; set; }
    public Guid? RecipientUserId { get; set; }
    public Guid? ProductId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Tin nhắn gần nhất trong conversation.</summary>
    public ChatMessageResponse? LastMessage { get; set; }
}

public class ChatMessageResponse
{
    public Guid Id { get; set; }
    public Guid ChatboxId { get; set; }
    public Guid? SenderUserId { get; set; }
    public ChatRole SenderRole { get; set; }
    public string? SenderName { get; set; }
    public string? Content { get; set; }
    public bool IsFromAi { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Link ảnh đính kèm (nếu có).</summary>
    public List<string> Images { get; set; } = new();
}

public class SendMessageRequest
{
    /// <summary>Nội dung văn bản. Có thể null/trống nếu chỉ gửi ảnh.</summary>
    public string? Content { get; set; }

    /// <summary>Link ảnh đã upload (qua endpoint upload ảnh chat). Không gửi nhị phân ở đây.</summary>
    public List<string>? ImageUrls { get; set; }
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
    public Guid? SenderUserId { get; set; }
    public ChatRole SenderRole { get; set; }
    public string? SenderName { get; set; }
    public string? Content { get; set; }
    public bool IsFromAi { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Images { get; set; } = new();
}
