using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Recommendation;

/// <summary>
/// Nghề nghiệp của người dùng — bảng tra cứu MỞ (giống Vibe/Style), không phải enum: danh sách nghề
/// còn dài ra theo thị trường, khác <c>Aspiration</c> vốn cố định bằng 4 cung Bát Trạch.
///
/// <para>
/// Lưu trên <c>User</c> chứ không phải theo phiên: nghề nghiệp ổn định theo năm, và luồng vật phẩm
/// mang theo người cũng cần tới nó khi không có căn phòng nào để bám vào.
/// </para>
/// </summary>
public class Occupation : BaseEntity
{
    /// <summary>Khoá tự nhiên bất biến, vd <c>"IT"</c>. Seeder và test tham chiếu bằng mã này.</summary>
    public string Code { get; set; } = null!;

    public string NameVi { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Do seeder tạo ra hay do admin thêm tay. Seeder chỉ được đụng vào row của chính nó — row admin
    /// tạo phải sống sót qua mọi lần deploy.
    /// </summary>
    public bool IsSystemSeeded { get; set; }

    public int SortOrder { get; set; }

    public ICollection<OccupationElementModifier> Modifiers { get; set; } = new List<OccupationElementModifier>();
}
