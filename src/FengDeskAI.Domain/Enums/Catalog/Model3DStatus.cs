namespace FengDeskAI.Domain.Enums.Catalog;

/// <summary>Trạng thái sinh model 3D từ ảnh sản phẩm (qua Meshy AI).</summary>
public enum Model3DStatus
{
    /// <summary>Đã tạo bản ghi, chưa gửi đi (hiếm — thường nhảy thẳng sang Processing).</summary>
    Pending = 0,

    /// <summary>Đã gửi job sang Meshy, đang chờ render (worker nền sẽ poll).</summary>
    Processing = 1,

    /// <summary>Hoàn tất — <c>ModelUrl</c> trỏ tới file GLB đã re-host trên storage.</summary>
    Succeeded = 2,

    /// <summary>Thất bại — xem <c>ErrorMessage</c>.</summary>
    Failed = 3,
}
