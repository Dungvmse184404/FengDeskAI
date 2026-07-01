namespace FengDeskAI.Domain.Enums.Notification
{
    public enum ReferenceType
    {
        None = 0,

        Order = 1,          // Thông báo liên quan đến đơn hàng (Đặt hàng thành công, Hủy đơn...)

        Delivery = 2,       // Thông báo tiến độ vận chuyển (Đang giao, Đã giao, Cập nhật trạng thái từ webhook)

        Payment = 3,        // Thông báo thanh toán (Xác nhận đã nhận tiền qua VNPay/PayOS, Thất bại)

        CustomerDesign = 4, // Thông báo về bản thiết kế cây cảnh tùy chỉnh (Đã duyệt thiết kế, Yêu cầu sửa đổi)

        Promotion = 5,      // Thông báo hệ thống (Tặng Voucher mới, Chiến dịch khuyến mãi)

        System = 6,         // Các thông báo bảo trì, cảnh báo tài khoản chung

        Return = 7,         // Thông báo yêu cầu trả hàng/đổi trả (Đã duyệt, Từ chối, Đã nhận hàng trả...)

        Refund = 8,         // Thông báo hoàn tiền (Đã hoàn tiền về nguồn/chuyển khoản)

        StaffInvitation = 9 // Lời mời làm nhân viên store — ReferenceId = GardenStaffAssignment.Id
    }
}
