using FengDeskAI.Domain.Enums.Vendor;

namespace FengDeskAI.Application.Features.Vendor.DTOs;

public class StoreAddressResponse
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid WardId { get; set; }
    public string StreetAddress { get; set; } = null!;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsActive { get; set; }
    /// <summary>Tên người gửi gửi cho nhà vận chuyển. Trống → dùng tên cửa hàng.</summary>
    public string? SenderName { get; set; }
    /// <summary>SĐT người gửi gửi cho nhà vận chuyển (di động 10 số). Trống → dùng hotline cửa hàng.</summary>
    public string? SenderPhone { get; set; }
}

public class StoreResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Hotline { get; set; } = null!;
    public string? OpeningHours { get; set; }
    public bool IsActive { get; set; }
    /// <summary>True nếu user gọi /stores/mine là owner của store này; false = chỉ là nhân viên (Accepted). Chỉ set ở GetMine.</summary>
    public bool IsOwner { get; set; }
    public StoreAddressResponse? Address { get; set; }
    public List<StoreOwnerResponse> Owners { get; set; } = new();
    /// <summary>Điểm đánh giá cửa hàng (từ đánh giá sản phẩm). Chỉ set ở GET /stores/{id}; null ở các danh sách.</summary>
    public StoreRatingResponse? Rating { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Trung bình sao (làm tròn 1 chữ số) + số lượt đánh giá của cửa hàng.</summary>
public class StoreRatingResponse
{
    public double Average { get; set; }
    public int Count { get; set; }
}

public class StoreOwnerResponse
{
    public Guid OwnerUserId { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime AssignedAt { get; set; }
}

public class CreateStoreRequest
{
    // Owner = người gọi (self-service); không nhận OwnerUserId từ client.
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Hotline { get; set; } = null!;
    public string? OpeningHours { get; set; }
}

public class AddOwnerRequest
{
    public Guid OwnerUserId { get; set; }
}

public class UpdateStoreRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Hotline { get; set; } = null!;
    public string? OpeningHours { get; set; }
    public bool IsActive { get; set; }
}

public class CreateStoreAddressRequest
{
    public Guid WardId { get; set; }
    public string StreetAddress { get; set; } = null!;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    /// <summary>Tên người gửi cho nhà vận chuyển. Bỏ trống → dùng tên cửa hàng.</summary>
    public string? SenderName { get; set; }
    /// <summary>
    /// SĐT người gửi cho nhà vận chuyển — phải là di động VN 10 số. Bắt buộc khi hotline cửa hàng
    /// không phải số di động (1900/số cố định) vì GHN từ chối các số đó.
    /// </summary>
    public string? SenderPhone { get; set; }
}

public class UpdateStoreAddressRequest
{
    public Guid WardId { get; set; }
    public string StreetAddress { get; set; } = null!;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    /// <summary>Tên người gửi cho nhà vận chuyển. Bỏ trống → dùng tên cửa hàng.</summary>
    public string? SenderName { get; set; }
    /// <summary>SĐT người gửi cho nhà vận chuyển — phải là di động VN 10 số. Xem <see cref="CreateStoreAddressRequest.SenderPhone"/>.</summary>
    public string? SenderPhone { get; set; }
}

public class AssignStaffRequest
{
    /// <summary>ID user lấy từ /api/users/search. Required.</summary>
    public Guid? StaffId { get; set; }
    /// <summary>Tra cứu theo email (cách phụ — vẫn hỗ trợ để FE cũ dùng).</summary>
    public string? StaffEmail { get; set; }
}

public class StaffAssignmentResponse
{
    public Guid Id { get; set; }
    public Guid GardenStoreId { get; set; }
    public Guid StaffId { get; set; }
    public string StaffName { get; set; } = null!;
    public string StaffEmail { get; set; } = null!;
    public string? StaffPhone { get; set; }
    public Guid InvitedBy { get; set; }
    public string? InvitedByName { get; set; }
    public InvitationStatus Status { get; set; }
    public DateTime InvitedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime? UnassignedAt { get; set; }
}

/// <summary>Lời mời gửi cho user hiện tại — để hiển thị ở MyInvitationsPage.</summary>
public class InvitationResponse
{
    public Guid Id { get; set; }
    public Guid GardenStoreId { get; set; }
    public string StoreName { get; set; } = null!;
    public Guid InvitedBy { get; set; }
    public string? InvitedByName { get; set; }
    public InvitationStatus Status { get; set; }
    public DateTime InvitedAt { get; set; }
}

/// <summary>
/// Vai trò của user hiện tại đối với 1 store — nguồn sự thật để FE ẩn/hiện tab.
/// Owner (chính/đồng sở hữu) full quyền; Staff (Accepted) chỉ xử lý đơn/ship,
/// KHÔNG sửa hồ sơ, KHÔNG xem thống kê/nhân viên.
/// </summary>
public class StoreMembershipResponse
{
    /// <summary>Owner chính (IsPrimary) của store.</summary>
    public bool IsPrimaryOwner { get; set; }
    /// <summary>Owner chính hoặc đồng sở hữu.</summary>
    public bool IsOwner { get; set; }
    /// <summary>Garden staff với assignment Accepted (chỉ tính khi không phải owner).</summary>
    public bool IsStaff { get; set; }
    /// <summary>Platform admin.</summary>
    public bool IsAdmin { get; set; }
    /// <summary>Được thao tác nghiệp vụ trên store (owner | staff | admin).</summary>
    public bool CanManage { get; set; }
}

/// <summary>Thống kê cửa hàng cho dashboard vendor (chỉ owner/admin).</summary>
/// <summary>
/// Số dư sổ cái các cửa hàng user SỞ HỮU (nhân viên không thấy) — menu tài khoản. <c>Available</c> = đã qua khoảng
/// giữ, <c>Pending</c> = còn trong khoảng giữ; cả hai đã trừ phí sàn và công nợ hoàn hàng.
/// </summary>
public class MyStoreBalanceResponse
{
    public decimal Available { get; set; }
    public decimal Pending { get; set; }
    public decimal Balance { get; set; }
    public int PayoutHoldDays { get; set; }
    public List<StoreBalanceResponse> Stores { get; set; } = [];
}

public class StoreBalanceResponse
{
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal Available { get; set; }
    public decimal Pending { get; set; }
    public decimal Balance { get; set; }
}

public class StoreStatisticsResponse
{
    /// <summary>Tổng doanh thu hàng (Subtotal) của các delivery đã Delivered.</summary>
    public decimal TotalRevenue { get; set; }
    /// <summary>Tổng phí ship của các delivery đã Delivered.</summary>
    public decimal TotalShippingFee { get; set; }
    public int TotalDeliveries { get; set; }
    /// <summary>Đếm delivery theo trạng thái (Pending/Preparing/Shipped/Delivered/…).</summary>
    public Dictionary<string, int> DeliveriesByStatus { get; set; } = new();
    /// <summary>Delivery chưa xong việc: Pending/Confirmed/Preparing/Shipped (đã có đơn, chưa giao tới tay khách).</summary>
    public int ActiveDeliveries { get; set; }
    /// <summary>Giá trị hàng (Subtotal) của các delivery đang xử lý — doanh thu sắp về.</summary>
    public decimal ActiveDeliveriesValue { get; set; }
    /// <summary>
    /// Đơn online khách đã đặt nhưng CHƯA thanh toán (Order.Pending, PayOS) có hàng của store này. Delivery
    /// chỉ được tạo khi tiền về, nên nếu không đếm riêng thì store không hề thấy những đơn này.
    /// </summary>
    public int AwaitingPaymentOrders { get; set; }
    /// <summary>
    /// Tiền CHƯA thu được: phần hàng trong đơn online chưa trả + đơn <b>COD đang trên đường</b> (COD thu
    /// tại điểm giao, nên hàng chưa tới tay khách nghĩa là chưa có đồng nào).
    /// </summary>
    public decimal AwaitingPaymentValue { get; set; }
    public int ProductCount { get; set; }
    /// <summary>Số nhân viên Accepted.</summary>
    public int StaffCount { get; set; }
    /// <summary>Doanh thu 6 tháng gần nhất (gồm tháng hiện tại). Giữ cho client cũ; client mới đọc <see cref="RevenueSeries"/>.</summary>
    public List<MonthlyRevenuePoint> RevenueByMonth { get; set; } = new();

    /// <summary>Khoảng thời gian đang xem (<c>week|month|quarter|year</c>) — echo lại đúng giá trị đã áp.</summary>
    public string Range { get; set; } = "month";
    /// <summary>Doanh thu theo mốc thời gian của <see cref="Range"/>; mốc rỗng vẫn có mặt để cột không bị hụt.</summary>
    public List<RevenueBucket> RevenueSeries { get; set; } = new();

    /// <summary>
    /// Cả bốn mốc (<c>week/month/quarter/year</c>) dựng sẵn từ CÙNG một bộ dữ liệu. Đổi mốc không đổi số
    /// liệu, chỉ đổi cách chia cột — nên client đổi tại chỗ thay vì gọi lại API (mỗi lần gọi là ~8 lượt
    /// đi về DB ở Sydney). <see cref="RevenueSeries"/> chính là phần tử ứng với <see cref="Range"/>.
    /// </summary>
    public Dictionary<string, List<RevenueBucket>> RevenueSeriesByRange { get; set; } = new();

    /// <summary>
    /// Sản phẩm đang nằm trong các đơn, kèm trạng thái tiền của đơn đó (<c>Ordered|Paid|Completed|Refunded</c>).
    /// Một sản phẩm xuất hiện nhiều dòng nếu nó đang ở nhiều trạng thái khác nhau.
    /// </summary>
    public List<StoreStatisticsItemRow> ItemsByStatus { get; set; } = new();
    /// <summary>Phí ship theo cùng bộ trạng thái — phí thuộc về ĐƠN nên không chia được xuống từng sản phẩm.</summary>
    public Dictionary<string, decimal> ShippingFeeByStatus { get; set; } = new();

    // ===== Đối soát (PayoutPolicy) =====
    /// <summary>Số ngày giữ tiền sau khi giao thành công — echo lại <c>PayoutPolicy.HoldDays</c>.</summary>
    public int PayoutHoldDays { get; set; }
    /// <summary>Tiền hàng đã qua hết khoảng giữ — phần nhà vườn có thể yêu cầu chi.</summary>
    public decimal AvailableForPayoutValue { get; set; }
    /// <summary>Đã giao nhưng CHƯA hết khoảng giữ — sẽ vào "có thể rút" khi đủ ngày.</summary>
    public decimal PendingClearanceValue { get; set; }
    /// <summary>Công nợ chưa được miễn (Pending/Disputed/Settled) sẽ trừ vào kỳ chi kế tiếp.</summary>
    public decimal OutstandingLiabilityValue { get; set; }

    // ===================== Sổ cái (docs/adr/platform-fee-ledger.md) =====================

    /// <summary>Tỉ lệ phí sàn đang áp cho đơn MỚI (Manager đặt, <c>platform_fee_rates</c>). Đơn cũ giữ tỉ lệ đã chốt.</summary>
    public decimal CommissionRate { get; set; }
    /// <summary>Σ phí sàn đã thu trên hàng đã giao, sau khi trả lại phần của hàng bị hoàn.</summary>
    public decimal PlatformCommission { get; set; }
    /// <summary>
    /// Số dư sổ nhà vườn: tiền sàn đang nợ vườn = tiền hàng đã giao − phí sàn − công nợ hoàn hàng. Đây là
    /// "thực nhận" — khác <see cref="TotalRevenue"/> (doanh thu gộp).
    /// </summary>
    public decimal LedgerBalance { get; set; }
    /// <summary>Phần số dư đã qua khoảng giữ <see cref="PayoutHoldDays"/> — có thể chi.</summary>
    public decimal LedgerAvailable { get; set; }
    /// <summary>Phần số dư còn trong khoảng giữ.</summary>
    public decimal LedgerPending { get; set; }
}

/// <summary>
/// Một cột trên biểu đồ doanh thu — mốc bắt đầu + nhãn đã dựng sẵn để FE không tự đoán định dạng.
///
/// <para>
/// Cột chồng 4 lớp theo **mức chắc chắn của tiền**, không phải 4 loại đơn rời rạc: đặt chưa trả (có thể
/// bốc hơi) → đã trả nhưng chưa giao xong (gần chắc) → đã xong (chắc) → hoàn tiền (tiền chảy ngược).
/// Mỗi lớp bucket theo mốc thời gian RIÊNG của nó (đặt hàng / tạo giao / giao xong / hoàn xong), nên tổng
/// một cột KHÔNG phải doanh thu của mốc đó mà là "trạng thái tiền phát sinh trong mốc đó".
/// </para>
/// </summary>
public class RevenueBucket
{
    /// <summary>Đầu mốc (UTC): ngày / đầu tuần / đầu tháng tuỳ range.</summary>
    public DateTime Start { get; set; }
    /// <summary>Nhãn hiển thị, vd <c>"12/09"</c> (ngày), <c>"Tuần 08/09"</c>, <c>"Th 09"</c>.</summary>
    public string LabelVi { get; set; } = "";

    /// <summary>Tiền hàng của đơn đã giao xong trong mốc (= <see cref="Completed"/>). Giữ tên cũ cho client cũ.</summary>
    public decimal Revenue { get; set; }
    public int DeliveredCount { get; set; }

    /// <summary>Đặt trong mốc nhưng CHƯA thanh toán — chưa chắc thành tiền.</summary>
    public decimal AwaitingPayment { get; set; }
    public int AwaitingPaymentCount { get; set; }
    /// <summary>Đã thanh toán/COD, đơn giao tạo trong mốc nhưng khâu giao nhận CHƯA xong.</summary>
    public decimal InProgress { get; set; }
    public int InProgressCount { get; set; }
    /// <summary>Đã giao tới tay khách trong mốc — tiền chắc chắn.</summary>
    public decimal Completed { get; set; }
    public int CompletedCount { get; set; }
    /// <summary>Hoàn tiền xong trong mốc (RMA) — tiền chảy ngược, vẽ tách hẳn chứ không trừ vào cột.</summary>
    public decimal Refunded { get; set; }
    public int RefundedCount { get; set; }
}

/// <summary>Một dòng "sản phẩm trong đơn" — gộp theo (sản phẩm × trạng thái), không phải theo đơn.</summary>
public class StoreStatisticsItemRow
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    /// <summary>
    /// <c>Ordered</c> (đặt, chưa trả tiền — gồm cả COD đang giao) · <c>Paid</c> (đã trả online, đang
    /// giao) · <c>Completed</c> (đã giao; COD coi như thu được tiền ở bước này) ·
    /// <c>Refunded</c>. Giữ nguyên mã tiếng Anh: FE hiển thị thành chip ngắn, dịch ở FE.
    /// </summary>
    public string Status { get; set; } = "";
    /// <summary>Tổng số lượng trong các đơn thuộc nhóm này.</summary>
    public int Quantity { get; set; }
    /// <summary>Tổng tiền hàng (đơn giá × số lượng).</summary>
    public decimal Value { get; set; }
    /// <summary>
    /// Phí ship phân bổ cho dòng này: <c>Σ phíShip(đơn) × tiềnDòng / tiềnHàng(đơn)</c>. Phí thuộc về đơn
    /// nên đây là phân bổ theo tỉ trọng, không phải phí riêng của sản phẩm — cộng mọi dòng của một đơn
    /// lại đúng bằng phí ship của đơn đó.
    /// </summary>
    public decimal ShippingFee { get; set; }
    /// <summary>Số đơn (delivery hoặc order) đang chứa sản phẩm này.</summary>
    public int OrderCount { get; set; }
}

public class MonthlyRevenuePoint
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Revenue { get; set; }
    public int DeliveredCount { get; set; }
}

/// <summary>
/// Chính sách phí sàn công khai — FE dùng để người bán xem trước "giá thực nhận" khi nhập giá sản phẩm.
/// Làm tròn phí sàn: tới đồng, nửa đồng làm tròn lên (<c>PlatformFeePolicy.ComputeCommission</c>).
/// </summary>
public class PlatformFeePolicyResponse
{
    /// <summary>Tỉ lệ phí sàn trên tiền hàng, vd 0.08.</summary>
    public decimal CommissionRate { get; set; }
    /// <summary>Trần giảm giá sàn tài trợ trên mỗi delivery (tỉ lệ trên tiền hàng) — bằng phí sàn.</summary>
    public decimal MaxPlatformFundedDiscountRate { get; set; }
    /// <summary>Số ngày giữ tiền sau khi giao trước khi nhà vườn được chi.</summary>
    public int PayoutHoldDays { get; set; }
    /// <summary>Tỉ lệ hiện tại áp từ lúc nào; null = mặc định hệ thống (chưa ai đặt).</summary>
    public DateTime? EffectiveFrom { get; set; }
}

/// <summary>Manager đổi phí sàn. Chỉ áp cho đơn đặt SAU thời điểm lưu — đơn cũ giữ tỉ lệ đã chốt.</summary>
public class UpdatePlatformFeeRequest
{
    /// <summary>
    /// Tỉ lệ trên tiền hàng, 0 – 0.3, tối đa 4 chữ số thập phân (0.0825 = 8,25%). Nullable để body thiếu trường
    /// bị từ chối thay vì âm thầm thành 0% (0 là mức hợp lệ).
    /// </summary>
    public decimal? CommissionRate { get; set; }
    public string? Note { get; set; }
}

public class PlatformFeeRateHistoryResponse
{
    public Guid Id { get; set; }
    public decimal CommissionRate { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public string? Note { get; set; }
    public string? ChangedByName { get; set; }
}
