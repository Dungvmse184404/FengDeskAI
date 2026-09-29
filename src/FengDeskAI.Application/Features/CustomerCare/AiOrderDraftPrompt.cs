using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.Application.Features.CustomerCare;

public static class AiOrderDraftPrompt
{
    /// <summary>
    /// Block "CURRENT ORDER DRAFT" gắn vào prompt mỗi lượt khi phòng có draft đang mở — nguồn sự thật về
    /// những gì user đã chọn (sản phẩm, số lượng, địa chỉ, thanh toán) để AI không phải nhớ qua lịch sử.
    /// </summary>
    public static string Build(AiOrderDraftRef draft, DateTime now)
    {
        var minutesLeft = Math.Max(1, (int)Math.Ceiling((draft.ExpiresAt - now).TotalMinutes));
        // '|' trong dữ liệu sẽ làm vỡ bảng markdown.
        static string Cell(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Replace('|', '/');

        return "## CURRENT ORDER DRAFT\n" +
            $"The user has an open draft order - NOT placed yet, expires in {minutesLeft} min. " +
            "This is the source of truth for what they chose; trust it over older chat messages.\n\n" +
            "| Product | Variant | Qty | Unit price | Shipping fee | Total | Address | Payment |\n" +
            "|---|---|---|---|---|---|---|---|\n" +
            $"| [{Cell(draft.ProductName)}](/products/{draft.ProductId}) | {Cell(draft.VariantName)} | {draft.Quantity} | " +
            $"{draft.UnitPrice:#,0}đ | {draft.ShippingFee:#,0}đ | {draft.TotalAmount:#,0}đ | {Cell(draft.AddressText)} | {draft.PaymentMethod} |\n\n" +
            "- The user agrees (\"ok\", \"chốt\", \"đặt đi\", \"yes\"...) -> call `confirm_order` now. Do NOT call `prepare_order` again, do NOT ask to confirm again.\n" +
            "- The user wants to change product / variant / quantity / address / payment -> call `prepare_order` with ONLY the changed fields.\n" +
            "- The user no longer wants it -> call `discard_order_draft`.\n" +
            "- The user talks about something else -> answer normally; mention the open draft only when relevant.";
    }
}
