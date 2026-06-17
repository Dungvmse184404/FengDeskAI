namespace FengDeskAI.Application.Features.CustomerCare.DTOs;

/// <summary>Yêu cầu gửi 1 tin nhắn tới trợ lý AI. Hội thoại được lưu vào chatboxes/chat_messages.</summary>
public sealed record AiChatRequest
{
    /// <summary>Hội thoại AI. Bỏ trống → lấy/tạo theo (user, ProductId); server trả lại ChatboxId.</summary>
    public Guid? ChatboxId { get; init; }

    /// <summary>Nội dung người dùng nhập. Có thể null nếu chỉ gửi ảnh.</summary>
    public string? Message { get; init; }

    /// <summary>Model muốn dùng cho lượt này. Bỏ trống → dùng model mặc định trong cấu hình.</summary>
    public string? Model { get; init; }

    /// <summary>Sản phẩm muốn hỏi AI (nếu có) — nạp thông tin sản phẩm làm ngữ cảnh.</summary>
    public Guid? ProductId { get; init; }

    /// <summary>Link ảnh đính kèm. Chỉ lưu link; AI nhận bản base64 tải từ link.</summary>
    public List<string>? ImageUrls { get; init; }
}

/// <summary>Một dòng trong lịch sử hội thoại trả về client.</summary>
public sealed record AiChatTurn(string Role, string? Content, IReadOnlyList<string> Images);

/// <summary>Kết quả 1 lượt chat: câu trả lời + lịch sử (đã cắt còn N lượt gần nhất).</summary>
public sealed record AiChatResponse
{
    public required Guid ChatboxId { get; init; }
    public required string Model { get; init; }
    public required string Reply { get; init; }
    public required IReadOnlyList<AiChatTurn> History { get; init; }
}
