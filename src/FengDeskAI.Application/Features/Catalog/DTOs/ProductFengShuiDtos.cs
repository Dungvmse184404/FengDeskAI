using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.Catalog.DTOs;

/// <summary>Khai báo/ cập nhật thuộc tính phong thủy cho 1 sản phẩm (làm sản phẩm thành ứng viên gợi ý).</summary>
public class SetProductFengShuiRequest
{
    /// <summary>Hành chính (IsPrimary). Bắt buộc.</summary>
    public FengShuiElement PrimaryElement { get; set; }

    /// <summary>Các hành phụ (0..n). Trùng hành chính sẽ bị bỏ qua.</summary>
    public List<FengShuiElement> SecondaryElements { get; set; } = new();

    /// <summary>
    /// Vị trí/cách dùng — quyết định engine chấm điểm thế nào (xem
    /// <c>docs/adr/product-placement-personal-recommendation.md</c>). Bỏ trống → <c>Desk</c>.
    /// </summary>
    public ProductPlacement? Placement { get; set; }

    /// <summary>
    /// Mục tiêu phong thủy vendor ĐỀ XUẤT cho vật phẩm (Tài lộc, Sức khỏe…). Ghi vào
    /// <c>product_aspirations</c> ở trạng thái CHƯA duyệt — engine bỏ qua cho tới khi admin bật.
    /// Thẻ đã được duyệt trước đó KHÔNG bị hạ xuống khi vendor sửa lại danh sách.
    /// </summary>
    public List<Aspiration> Aspirations { get; set; } = new();

    /// <summary>Mã vibe (vibes.code), vd "Focus".</summary>
    public List<string> Vibes { get; set; } = new();
    /// <summary>Mã phong cách (styles.code), vd "Minimal".</summary>
    public List<string> Styles { get; set; } = new();
}

/// <summary>Admin/manager duyệt thẻ mục tiêu — chỉ thẻ đã duyệt mới tham gia lọc gợi ý.</summary>
public class ApproveProductAspirationsRequest
{
    /// <summary>Danh sách thẻ được duyệt. Thẻ không có trong danh sách bị HẠ về chưa duyệt.</summary>
    public List<Aspiration> Approved { get; set; } = new();
}

public class ProductFengShuiResponse
{
    public Guid ProductId { get; set; }
    public FengShuiElement PrimaryElement { get; set; }
    public List<FengShuiElement> SecondaryElements { get; set; } = new();
    public ProductPlacement Placement { get; set; }

    /// <summary>Thẻ mục tiêu ĐÃ được admin duyệt — chỉ những thẻ này tham gia lọc gợi ý.</summary>
    public List<Aspiration> ApprovedAspirations { get; set; } = new();

    /// <summary>Thẻ mục tiêu vendor đề xuất nhưng CHƯA duyệt.</summary>
    public List<Aspiration> PendingAspirations { get; set; } = new();

    /// <summary>Mã vibe (vibes.code), vd "Focus".</summary>
    public List<string> Vibes { get; set; } = new();
    /// <summary>Mã phong cách (styles.code), vd "Minimal".</summary>
    public List<string> Styles { get; set; } = new();
}
