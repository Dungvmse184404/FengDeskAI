namespace FengDeskAI.Domain.Enums.Catalog;

/// <summary>Loại yêu cầu sinh model 3D — quyết định chạy tự động hay qua hàng chờ thủ công.</summary>
public enum Model3DRequestType
{
    /// <summary>Product chưa từng có model — tự động gọi Meshy (có retry khi hết credit).</summary>
    Initial = 0,

    /// <summary>Product đã có model, owner/garden staff muốn tạo lại — vào hàng chờ thủ công của staff sàn.</summary>
    Regenerate = 1,
}
