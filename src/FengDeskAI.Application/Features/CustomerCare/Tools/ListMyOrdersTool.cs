using System.Text.Json;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Features.Sales.Services;
using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>Lịch sử đơn hàng/thanh toán của CHÍNH user (scope theo userId).</summary>
public sealed class ListMyOrdersTool : IAiTool
{
    private const int MaxLimit = 10;
    private readonly IOrderService _orders;

    public ListMyOrdersTool(IOrderService orders) => _orders = orders;

    public string Name => "list_my_orders";
    public string Description => "Liệt kê đơn hàng gần nhất của người dùng (mã đơn, tổng tiền, trạng thái thanh toán/giao hàng) — dùng để kiểm tra lịch sử mua/thanh toán.";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>
    {
        ["limit"] = new("integer", $"Số đơn gần nhất (mặc định {MaxLimit})."),
    };

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var limit = Math.Clamp(ToolArgs.GetInt(arguments, "limit") ?? MaxLimit, 1, MaxLimit);
        var result = await _orders.GetMineAsync(context.UserId, new PageRequest { Page = 1, PageSize = limit }, ct);
        if (!result.IsSuccess || result.Data is null)
            return ToolArgs.Error(result.Message ?? "Không lấy được lịch sử đơn.");

        return ToolArgs.Json(new { total = result.Data.TotalCount, items = result.Data.Items });
    }
}
