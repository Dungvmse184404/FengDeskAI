namespace FengDeskAI.Domain.Enums.Payment;

/// <summary>Cổng/phương thức thanh toán (lưu DB string).</summary>
public enum PaymentMethod
{
    PayOS,
    COD, // thanh toán khi nhận hàng — không qua cổng thanh toán
}
