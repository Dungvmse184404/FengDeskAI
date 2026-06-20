using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Catalog;

namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>
/// Model 3D của một <see cref="Product"/> (quan hệ 1–1), sinh từ một ảnh sản phẩm qua Meshy AI.
/// Việc sinh là bất đồng bộ: lưu <see cref="MeshyTaskId"/> + <see cref="Status"/> = Processing,
/// worker nền poll Meshy rồi tải GLB và re-host lên storage, ghi <see cref="ModelUrl"/>.
/// </summary>
public class ProductModel3D : BaseEntity
{
    public Guid ProductId { get; set; }

    public Model3DStatus Status { get; set; } = Model3DStatus.Pending;

    /// <summary>Ảnh nguồn (URL công khai) dùng để sinh model — Meshy tự fetch URL này.</summary>
    public string SourceImageUrl { get; set; } = null!;

    /// <summary>Id job bên Meshy (dùng để poll trạng thái).</summary>
    public string? MeshyTaskId { get; set; }

    /// <summary>URL file GLB đã re-host trên storage (Supabase). Null cho tới khi Succeeded.</summary>
    public string? ModelUrl { get; set; }

    /// <summary>Ảnh thumbnail của model 3D (Meshy trả về). Có thể null.</summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>Tiến độ render 0–100 (Meshy báo).</summary>
    public int Progress { get; set; }

    /// <summary>Thông điệp lỗi khi <see cref="Status"/> = Failed.</summary>
    public string? ErrorMessage { get; set; }

    public Product Product { get; set; } = null!;
}
