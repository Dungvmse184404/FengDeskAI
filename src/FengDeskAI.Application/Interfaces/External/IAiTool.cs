using System.Text.Json;

namespace FengDeskAI.Application.Interfaces.External;

/// <summary>Mô tả 1 tham số của tool (JSON schema rút gọn).</summary>
public sealed record AiToolParameter(string Type, string Description, bool Required = false, IReadOnlyList<string>? Enum = null);

/// <summary>Khai báo tool gửi cho LLM (không gồm phần thực thi) — transport dùng cái này.</summary>
public sealed record AiToolSpec(string Name, string Description, IReadOnlyDictionary<string, AiToolParameter> Parameters);

/// <summary>1 yêu cầu gọi tool do LLM trả về.</summary>
public sealed record AiToolCall(string Name, string ArgumentsJson);

/// <summary>Sản phẩm mà tool đã trả về trong lượt chat — để hậu xử lý tự chèn link markdown nếu model quên.</summary>
public sealed record AiProductRef(Guid Id, string Name);

/// <summary>
/// Thanh toán PayOS do confirm_order tạo trong lượt chat. AiChatService gắn block máy-đọc-được vào
/// cuối tin nhắn AI (deterministic — không phó mặc model chép lại link) để FE render card QR/nút thanh toán.
/// </summary>
public sealed record AiPaymentRef(Guid OrderId, decimal Amount, string CheckoutUrl, string? QrCode, int ExpiresInMinutes);

/// <summary>
/// Ảnh chụp draft đơn hàng đang mở của phòng TẠI ĐẦU LƯỢT chat (bảng <c>ai_order_drafts</c>).
/// AiChatService dùng để gắn block "CURRENT ORDER DRAFT" vào prompt và để chọn/mô tả tool đặt hàng.
/// </summary>
public sealed record AiOrderDraftRef(
    Guid ProductId,
    string ProductName,
    string? VariantName,
    int Quantity,
    decimal UnitPrice,
    decimal ShippingFee,
    decimal TotalAmount,
    string AddressText,
    string PaymentMethod,
    DateTime ExpiresAt);

/// <summary>
/// Ngữ cảnh thực thi tool — scope theo user để không lộ dữ liệu người khác.
/// <paramref name="ChatboxId"/>: phòng đang hội thoại (dùng cho tool đọc thông tin đối phương theo consent).
/// <paramref name="IsPrivateRoom"/>: true = phòng riêng user↔AI (SendAsync); false = phòng chung nhiều người
/// (RespondInRoomAsync) — tool có tác dụng phụ (vd đặt hàng) chỉ được chạy khi true, chặn cả ở BuildToolSpecs
/// (không đưa vào danh sách tool cho LLM) lẫn ExecuteToolAsync (phòng model vẫn cố emit tool_call).
/// </summary>
public sealed record AiToolContext(Guid UserId, string? UserRole, string? UserEmail, Guid? ChatboxId = null, bool IsPrivateRoom = true)
{
    /// <summary>
    /// Registry per-turn: tool nào trả sản phẩm thì ghi vào đây (Id + Name).
    /// <c>AiChatService</c> dùng để auto-link "[Tên](/products/{id})" trong câu trả lời cuối — deterministic,
    /// không phó mặc cho model nhớ quy tắc hyperlink.
    /// </summary>
    public List<AiProductRef> Products { get; } = new();

    /// <summary>Thanh toán tạo trong lượt này (confirm_order + PayOS). Null = không có.</summary>
    public AiPaymentRef? Payment { get; set; }

    /// <summary>Draft đơn hàng đang mở lúc BẮT ĐẦU lượt (null = chưa có). Chỉ nạp ở phòng riêng.</summary>
    public AiOrderDraftRef? ActiveOrderDraft { get; init; }

    /// <summary>
    /// true khi draft vừa được tạo/sửa/bỏ TRONG lượt này — user chưa thấy phiên bản mới nên
    /// confirm_order phải từ chối (luật "không confirm cùng lượt với lúc hiện tóm tắt" do code đảm bảo).
    /// </summary>
    public bool OrderDraftChangedThisTurn { get; set; }
}

/// <summary>
/// Một công cụ AI có thể gọi (function calling). Thuần Application: gọi lại các service nghiệp vụ,
/// trả kết quả JSON (chuỗi) để feed ngược cho LLM. v1 chỉ tool ĐỌC, scope theo <see cref="AiToolContext"/>.
/// </summary>
public interface IAiTool
{
    string Name { get; }
    string Description { get; }
    IReadOnlyDictionary<string, AiToolParameter> Parameters { get; }
    Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default);

    /// <summary>Mô tả gửi LLM theo ngữ cảnh lượt chat (vd có/chưa có draft đơn hàng). Mặc định = <see cref="Description"/>.</summary>
    string DescribeFor(AiToolContext context) => Description;

    /// <summary>Có đưa tool này cho LLM trong lượt này không (vd confirm_order chỉ khi đã có draft). Mặc định true.</summary>
    bool IsAvailable(AiToolContext context) => true;
}

public static class AiToolExtensions
{
    public static AiToolSpec ToSpec(this IAiTool tool) => new(tool.Name, tool.Description, tool.Parameters);

    public static AiToolSpec ToSpec(this IAiTool tool, AiToolContext context)
        => new(tool.Name, tool.DescribeFor(context), tool.Parameters);
}
