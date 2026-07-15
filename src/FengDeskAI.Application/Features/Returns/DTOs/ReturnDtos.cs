using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.Enums.Sales;

namespace FengDeskAI.Application.Features.Returns.DTOs;

// ---------- Actor (role của caller, enforce ở tầng service) ----------

/// <summary>
/// Ngữ cảnh phân quyền của người gọi, dựng từ JWT claims ở controller và enforce lại trong service
/// (không chỉ dựa policy ở controller). <see cref="CanDecide"/> = Staff trở lên (ra quyết định ticket);
/// <see cref="CanManageRefund"/> = Manager trở lên (thực thi tài chính).
/// </summary>
public record RmaActor(Guid UserId, bool IsStaff, bool IsManager, bool IsAdmin, bool IsGardenOwner)
{
    public bool CanDecide => IsStaff || IsManager || IsAdmin;
    public bool CanManageRefund => IsManager || IsAdmin;
}

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
    public ReturnReason Reason { get; set; } = ReturnReason.WrongItem;
    public string? ReasonDetail { get; set; }

    public List<CreateReturnItemRequest> Items { get; set; } = new();

    /// <summary>Ảnh bằng chứng (URL đã upload sẵn) — có thể kèm cùng file upload.</summary>
    public List<string>? ImageUrls { get; set; }

    // Bắt buộc khi đơn COD + hoàn tiền (hoàn qua chuyển khoản).
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
}

/// <summary>Một file ảnh bằng chứng đã đọc vào stream — controller dựng từ IFormFile rồi truyền xuống service.</summary>
public record ReturnImageFile(System.IO.Stream Content, string FileName, string ContentType);

public class RequestMoreEvidenceRequest
{
    public string? Note { get; set; }
    /// <summary>Số giờ cho khách bổ sung bằng chứng (mặc định 48h).</summary>
    public int? DeadlineHours { get; set; }
}

public class RejectReturnRequest
{
    public string Reason { get; set; } = null!;
}

public class ApproveRefundRequest
{
    /// <summary>Hoàn kho hàng trả nếu đã nhận & kiểm đạt (chỉ áp dụng lý do hàng vật lý).</summary>
    public bool Restock { get; set; } = true;
    public string? Note { get; set; }
}

public class ApproveExchangeRequest
{
    public bool Restock { get; set; } = true;
    public string? Note { get; set; }
}

public class VendorDisputeRequest
{
    public string Reason { get; set; } = null!;
}

// ---------- Refund (Manager) ----------

public class ManagerConfirmRefundRequest
{
    /// <summary>Lý do can thiệp thủ công — BẮT BUỘC (audit trail).</summary>
    public string ManualReason { get; set; } = null!;
    /// <summary>URL bằng chứng đã chuyển tiền — BẮT BUỘC (audit trail).</summary>
    public string EvidenceUrl { get; set; } = null!;
}

// ---------- Vendor liability (Manager) ----------

public class ResolveLiabilityRequest
{
    /// <summary>true = vendor đúng → miễn khoản trừ (waived); false = vendor sai → giữ khoản trừ (settled).</summary>
    public bool VendorWins { get; set; }
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
    public string Gateway { get; set; } = null!;
    public string? ProviderRefundId { get; set; }
    public int RetryCount { get; set; }
    public bool IsManual { get; set; }
    public string? ManualReason { get; set; }
    public string? EvidenceUrl { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class VendorLiabilityResponse
{
    public Guid Id { get; set; }
    public Guid GardenStoreId { get; set; }
    public Guid ReturnRequestId { get; set; }
    public Guid? RefundId { get; set; }
    public decimal Amount { get; set; }
    public VendorLiabilityStatus Status { get; set; }
    public string? DisputeReason { get; set; }
    public DateTime DisputeDeadline { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
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

    public VendorResponse VendorResponse { get; set; }
    public DateTime? VendorResponseDeadline { get; set; }
    public DateTime? EvidenceDeadline { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? RejectedReason { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public Guid? ReplacementDeliveryId { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<ReturnItemResponse> Items { get; set; } = new();
    public List<string> ImageUrls { get; set; } = new();
    public List<ReturnStatusLogResponse> StatusLogs { get; set; } = new();
    public RefundResponse? Refund { get; set; }
    public VendorLiabilityResponse? VendorLiability { get; set; }
}
