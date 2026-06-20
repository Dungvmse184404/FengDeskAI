namespace FengDeskAI.Application.Interfaces.External;

/// <summary>Trạng thái thô của một job sinh 3D từ phía provider (chuẩn hoá, không phụ thuộc Meshy).</summary>
public enum Model3DGenerationState
{
    /// <summary>Đang xử lý (pending/in-progress).</summary>
    Running = 0,

    /// <summary>Hoàn tất — <see cref="Model3DTaskResult.GlbUrl"/> sẵn sàng tải.</summary>
    Succeeded = 1,

    /// <summary>Thất bại/huỷ/hết hạn — xem <see cref="Model3DTaskResult.Error"/>.</summary>
    Failed = 2,
}

/// <summary>Kết quả 1 lần poll job sinh 3D.</summary>
/// <param name="State">Trạng thái chuẩn hoá.</param>
/// <param name="Progress">Tiến độ 0–100.</param>
/// <param name="GlbUrl">URL file GLB (chỉ có khi Succeeded). Là URL tạm của provider.</param>
/// <param name="ThumbnailUrl">URL thumbnail (nếu có).</param>
/// <param name="Error">Thông điệp lỗi (khi Failed).</param>
public sealed record Model3DTaskResult(
    Model3DGenerationState State, int Progress, string? GlbUrl, string? ThumbnailUrl, string? Error);

/// <summary>
/// Sinh model 3D từ ảnh — impl mặc định gọi Meshy AI (image-to-3D, bất đồng bộ).
/// Bật/tắt mock bằng <c>MeshySettings.UseMock</c>. Job chạy ngầm: caller start → nhận taskId,
/// worker nền poll qua <see cref="GetTaskAsync"/> tới khi Succeeded rồi tải GLB qua <see cref="DownloadAsync"/>.
/// </summary>
public interface IModel3DGenerator
{
    /// <summary>Gửi job image-to-3D với ảnh nguồn (URL công khai). Trả về taskId của provider.</summary>
    Task<string> StartImageTo3DAsync(string imageUrl, CancellationToken ct = default);

    /// <summary>Poll trạng thái 1 job.</summary>
    Task<Model3DTaskResult> GetTaskAsync(string taskId, CancellationToken ct = default);

    /// <summary>Tải nội dung file 3D (GLB) từ URL provider về stream để re-host sang storage.</summary>
    Task<Stream> DownloadAsync(string url, CancellationToken ct = default);
}
