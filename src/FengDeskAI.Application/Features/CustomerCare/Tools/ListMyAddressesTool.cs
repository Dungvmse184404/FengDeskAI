using System.Text.Json;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>
/// Liệt kê địa chỉ giao hàng đã lưu của CHÍNH user (scope theo userId) — để AI đọc cho user chọn
/// trước khi gọi prepare_order với shippingAddressId cụ thể (thay vì luôn dùng địa chỉ mặc định).
/// </summary>
public sealed class ListMyAddressesTool : IAiTool
{
    private readonly IUnitOfWork _uow;

    public ListMyAddressesTool(IUnitOfWork uow) => _uow = uow;

    public string Name => "list_my_addresses";

    public string Description =>
        "List the current user's saved shipping addresses (id, label, recipient, full address text, isDefault). " +
        "Use this when the user wants to choose a different delivery address than their default, or asks what " +
        "addresses they have saved. Read the list back to the user, then pass the chosen address's id as " +
        "'shippingAddressId' to prepare_order. If the list is empty, tell them to add one at /profile/addresses.";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>();

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var addresses = await _uow.UserAddresses.GetByUserIdWithWardChainAsync(context.UserId, ct);

        return ToolArgs.Json(new
        {
            addresses = addresses.Select(a => new
            {
                id = a.Id,
                label = a.Label,
                isDefault = a.IsDefault,
                recipientName = a.RecipientName,
                recipientPhone = a.RecipientPhone,
                addressText = $"{a.StreetAddress}, {a.Ward.Name}, {a.Ward.District.Name}, {a.Ward.District.Province.Name}",
            }),
            fixLinks = addresses.Count == 0 ? new { address = "/profile/addresses" } : null,
            note = addresses.Count == 0
                ? "The user has no saved address. Tell them to add one at the link."
                : "Read the options back to the user (label/recipient/address). Once they pick one, call " +
                  "prepare_order again with shippingAddressId set to that address's id.",
        });
    }
}
