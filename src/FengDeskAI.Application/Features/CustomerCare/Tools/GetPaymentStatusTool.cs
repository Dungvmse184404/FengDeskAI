using System.Text.Json;
using FengDeskAI.Application.Features.Payment.Services;
using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>Kiểm tra trạng thái thanh toán của 1 đơn của CHÍNH user (scope theo userId).</summary>
public sealed class GetPaymentStatusTool : IAiTool
{
    private readonly IPaymentService _payments;

    public GetPaymentStatusTool(IPaymentService payments) => _payments = payments;

    public string Name => "get_payment_status";
    public string Description => "Check the payment status of an order by orderId (get orderId from list_my_orders).";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>
    {
        ["orderId"] = new("string", "Order id (GUID).", Required: true),
    };

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var orderId = ToolArgs.GetGuid(arguments, "orderId");
        if (orderId is null)
            return ToolArgs.Error("Missing or invalid 'orderId' (must be a GUID).");

        var result = await _payments.GetStatusAsync(orderId.Value, context.UserId, ct);
        if (!result.IsSuccess || result.Data is null)
            return ToolArgs.Error(result.Message ?? "Could not load the payment status.");

        return ToolArgs.Json(result.Data);
    }
}
