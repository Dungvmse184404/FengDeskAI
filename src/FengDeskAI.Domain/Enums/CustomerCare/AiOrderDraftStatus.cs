namespace FengDeskAI.Domain.Enums.CustomerCare;

/// <summary>
/// Trạng thái draft đơn hàng do trợ lý AI chuẩn bị. Chỉ có 2 giá trị — mọi nhánh kết thúc (đặt xong,
/// user bỏ, hết hạn) đều là XÓA CỨNG dòng, không có trạng thái "Confirmed/Cancelled".
/// </summary>
public enum AiOrderDraftStatus
{
    /// <summary>Đang mở — user có thể sửa, bỏ hoặc xác nhận.</summary>
    Pending,

    /// <summary>
    /// confirm_order đã "chiếm" draft và đang tạo đơn thật. Rời Pending nên không thể bị confirm lần hai;
    /// process chết giữa chừng thì worker dọn sau ConfirmingGraceMinutes.
    /// </summary>
    Confirming,
}
