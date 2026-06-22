namespace FengDeskAI.Domain.Enums.Sales;

/// <summary>Loại yêu cầu: hoàn tiền hay đổi hàng. Lưu DB dạng string.</summary>
public enum ReturnType
{
    Refund,   // trả hàng & hoàn tiền
    Exchange, // đổi sang sản phẩm/biến thể khác
}
