using System.Text.Json;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>
/// Bỏ (xóa cứng) draft đơn hàng đang mở của phòng khi user không muốn mua nữa. Chỉ đụng draft —
/// KHÔNG bao giờ hủy đơn thật đã đặt. Chỉ đưa cho LLM khi đầu lượt đã có draft, và chỉ ở phòng riêng.
/// </summary>
public sealed class DiscardOrderDraftTool : IAiTool
{
    private readonly IUnitOfWork _uow;

    public DiscardOrderDraftTool(IUnitOfWork uow) => _uow = uow;

    public string Name => "discard_order_draft";

    public string Description =>
        "Discard the user's CURRENT ORDER DRAFT when they say they no longer want it (\"thôi\", \"hủy đơn nháp\", " +
        "\"không mua nữa\"). Takes no parameters. Only removes the draft - it never cancels an order that was already placed. " +
        "If they just want to change something, edit the draft with prepare_order instead.";

    public bool IsAvailable(AiToolContext context) => context.ActiveOrderDraft is not null;

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>();

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        if (context.ChatboxId is not { } chatboxId)
            return ToolArgs.Error("Could not determine the chat room.");

        var deleted = await _uow.AiOrderDrafts.DeletePendingAsync(context.UserId, chatboxId, ct);
        context.OrderDraftChangedThisTurn = true;

        return deleted > 0
            ? ToolArgs.Json(new { discarded = true, note = "The draft order was discarded. Nothing was ordered." })
            : ToolArgs.Error("There is no open draft order to discard.");
    }
}
