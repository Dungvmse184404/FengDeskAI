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
/// Meshy trả <c>402 Payment Required</c> — hết credit ngay lúc gửi task (không phải lỗi xuất hiện
/// sau khi poll). Caller (worker/service) bắt riêng exception này để requeue + backoff thay vì đánh
/// dấu Failed. Xem <c>docs/adr/refactor-model3d-request-flow.md</c> mục 5.
/// </summary>
public sealed class InsufficientCreditsException : Exception
{
    public InsufficientCreditsException(string message) : base(message) { }
}

/// <summary>
/// Provider sinh model 3D từ chối request hoặc tạm thời không phục vụ. Giữ lại HTTP status và
/// thông điệp đã được giới hạn độ dài để tầng Application có thể trả lỗi hữu ích cho staff và log
/// được nguyên nhân thật, thay vì làm mất response khi gọi <c>EnsureSuccessStatusCode</c>.
/// </summary>
public sealed class Model3DProviderException : Exception
{
    public Model3DProviderException(int statusCode, string providerMessage)
        : base($"Model 3D provider trả HTTP {statusCode}: {providerMessage}")
    {
        StatusCode = statusCode;
        ProviderMessage = providerMessage;
    }

    public int StatusCode { get; }
    public string ProviderMessage { get; }
}

/// <summary>
/// Sinh model 3D từ ảnh — gọi Meshy AI (multi-image-to-3D, bất đồng bộ, 1–4 ảnh cùng 1 object từ
/// nhiều góc). Job chạy ngầm: caller start → nhận taskId, worker nền poll qua
/// <see cref="GetTaskAsync"/> tới khi Succeeded rồi tải GLB qua <see cref="DownloadAsync"/>.
/// </summary>
public interface IModel3DGenerator
{
    /// <summary>
    /// Gửi job multi-image-to-3D với 1–4 ảnh nguồn (URL công khai, cùng 1 vật thể nhiều góc).
    /// Trả về taskId của provider. Ném <see cref="InsufficientCreditsException"/> nếu Meshy trả 402.
    /// </summary>
    Task<string> StartImageTo3DAsync(IReadOnlyList<string> imageUrls, CancellationToken ct = default);

    /// <summary>Poll trạng thái 1 job.</summary>
    Task<Model3DTaskResult> GetTaskAsync(string taskId, CancellationToken ct = default);

    /// <summary>Tải nội dung file 3D (GLB) từ URL provider về stream để re-host sang storage.</summary>
    Task<Stream> DownloadAsync(string url, CancellationToken ct = default);

    /// <summary>
    /// Số phút backoff trước khi worker thử lại 1 request Initial sau khi gặp
    /// <see cref="InsufficientCreditsException"/> (cấu hình provider, vd <c>MeshySettings</c> ở tầng Infrastructure —
    /// expose qua đây để Application không phụ thuộc ngược vào Infrastructure).
    /// </summary>
    int InsufficientCreditsBackoffMinutes { get; }
}
