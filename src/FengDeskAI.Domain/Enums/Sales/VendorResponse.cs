namespace FengDeskAI.Domain.Enums.Sales;

/// <summary>
/// Phản hồi của vendor với một ticket trong khung SLA (non-blocking). Lưu DB dạng string.
/// Dù vendor phản đối, Staff vẫn toàn quyền quyết định; tranh chấp xử lý ở tầng công nợ (VendorLiability).
/// </summary>
public enum VendorResponse
{
    Pending,       // chưa phản hồi
    Acknowledged,  // vendor đồng ý/ghi nhận
    Disputed,      // vendor phản đối (không chặn khách)
}
