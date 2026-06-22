using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.Enums.Sales;

namespace FengDeskAI.Application.Features.Returns.DTOs;

// ---------- Requests ----------

public class CreateReturnItemRequest
{
    public Guid OrderItemId { get; set; }
    public int Quantity { get; set; } = 1;

    /// <summary>Biến thể muốn đổi sang — bắt buộc khi Type = Exchange.</summary>
    public Guid? ExchangeProductItemId { get; set; }
}

public class CreateReturnRequest
{
    public Guid DeliveryId { get; set; }
    public ReturnType Type { get; set; } = ReturnType.Refund;
    public ReturnReason Reason { get; set; } = ReturnReason.Other;
    public string? ReasonDetail { get; set; }

    public List<CreateReturnItemRequest> Items { get; set; } = new();

    /// <summary>Ảnh bằng chứng (URL đã upload sẵn).</summary>
    public List<string>? ImageUrls { get; set; }

    // Bắt buộc khi đơn COD + hoàn tiền (hoàn qua chuyển khoản).
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
}

public class ShipBackRequest
{
    public string TrackingCode { get; set; } = null!;
}

public class ApproveReturnRequest
{
    public string? Note { get; set; }
}

public class RejectReturnRequest
{
    public string Reason { get; set; } = null!;
}

public class ResolveReturnRequest
{
    /// <summary>Hoàn kho hàng trả khi đã kiểm và hàng còn bán được (mặc định true).</summary>
    public bool Restock { get; set; } = true;
    public string? Note { get; set; }
}

// ---------- Responses ----------

public class ReturnItemResponse
{
    public Guid Id { get; set; }
    public Guid OrderItemId { get; set; }
    public string ProductName { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public Guid? ExchangeProductItemId { get; set; }
}

public class RefundResponse
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public RefundMethod Method { get; set; }
    public RefundStatus Status { get; set; }
    public string? ProviderRefundId { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ReturnStatusLogResponse
{
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class ReturnListItemResponse
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid DeliveryId { get; set; }
    public ReturnType Type { get; set; }
    public ReturnRequestStatus Status { get; set; }
    public ReturnReason Reason { get; set; }
    public decimal RefundAmount { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReturnDetailResponse
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid DeliveryId { get; set; }
    public Guid CustomerId { get; set; }
    public ReturnType Type { get; set; }
    public ReturnRequestStatus Status { get; set; }
    public ReturnReason Reason { get; set; }
    public string? ReasonDetail { get; set; }
    public decimal RefundAmount { get; set; }
    public RefundMethod RefundMethod { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? ReturnTrackingCode { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectedReason { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public Guid? ReplacementDeliveryId { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<ReturnItemResponse> Items { get; set; } = new();
    public List<string> ImageUrls { get; set; } = new();
    public List<ReturnStatusLogResponse> StatusLogs { get; set; } = new();
    public RefundResponse? Refund { get; set; }
}
