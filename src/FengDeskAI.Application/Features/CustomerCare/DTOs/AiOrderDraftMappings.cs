using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Domain.Entities.CustomerCare;

namespace FengDeskAI.Application.Features.CustomerCare.DTOs;

/// <summary>Chuyển draft (entity) sang các dạng AI đọc: snapshot trong context và summary trong tool result.</summary>
public static class AiOrderDraftMappings
{
    public static AiOrderDraftRef ToRef(this AiOrderDraft draft) => new(
        draft.ProductId,
        draft.ProductName,
        draft.VariantName,
        draft.Quantity,
        draft.UnitPriceSnapshot,
        draft.ShippingFeeSnapshot,
        draft.TotalAmountSnapshot,
        draft.AddressText,
        draft.PaymentMethod.ToString(),
        draft.ExpiresAt);

    /// <summary>Summary gọn cho tool result — số đã tính sẵn để model khỏi tự cộng sai.</summary>
    public static object ToToolSummary(this AiOrderDraft draft) => new
    {
        productName = draft.ProductName,
        variant = draft.VariantName,
        quantity = draft.Quantity,
        unitPrice = draft.UnitPriceSnapshot,
        shippingFee = draft.ShippingFeeSnapshot,
        total = draft.TotalAmountSnapshot,
        addressText = draft.AddressText,
        paymentMethod = draft.PaymentMethod.ToString(),
    };
}
