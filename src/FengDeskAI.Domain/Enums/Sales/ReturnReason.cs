namespace FengDeskAI.Domain.Enums.Sales;

/// <summary>
/// Lý do khiếu nại/trả hàng (RMA v2). Lưu DB dạng string.
/// Quyết định luồng xử lý: <see cref="PlantHealth"/> (cây chết) KHÔNG thu hồi hàng —
/// bỏ qua ReturnInTransit/ItemReceived; ba lý do còn lại là hàng còn giá trị → phải thu hồi.
/// </summary>
public enum ReturnReason
{
    PlantHealth,     // cây chết/không khỏe — không thu hồi
    WrongItem,       // giao sai sản phẩm
    DamagedPackage,  // kiện hàng hư hại
    NotAsDescribed,  // không đúng mô tả
}
