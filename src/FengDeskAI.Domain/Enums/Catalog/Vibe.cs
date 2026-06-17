namespace FengDeskAI.Domain.Enums.Catalog;

/// <summary>
/// Cảm giác/không khí mà một sản phẩm tạo ra cho không gian làm việc.
/// Dùng match với mục tiêu tâm lý suy ra từ <c>WorkPurpose</c> (vd Office → Focus).
/// </summary>
public enum Vibe
{
    Focus,    // Tập trung
    Relax,    // Thư giãn
    Creative, // Kích thích sáng tạo
    Calm,     // Tĩnh tại
    Energize, // Tăng năng lượng
}
