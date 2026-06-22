namespace FengDeskAI.Domain.Enums.Payment;

/// <summary>Cách thức hoàn tiền. Lưu DB dạng string.</summary>
public enum RefundMethod
{
    Original,     // hoàn về nguồn thanh toán gốc (PayOS)
    BankTransfer, // chuyển khoản ngân hàng (đơn COD)
    Manual,       // xử lý thủ công khác
}
