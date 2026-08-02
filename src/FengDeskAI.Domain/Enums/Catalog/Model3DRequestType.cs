namespace FengDeskAI.Domain.Enums.Catalog;

/// <summary>Loại yêu cầu sinh model 3D; cả hai loại đều qua hàng chờ staff.</summary>
public enum Model3DRequestType
{
    /// <summary>Ảnh sản phẩm chưa từng có model.</summary>
    Initial = 0,

    /// <summary>Ảnh đã có model, owner/garden staff muốn tạo lại.</summary>
    Regenerate = 1,
}
