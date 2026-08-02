namespace FengDeskAI.Application.Features.Catalog.DTOs;

/// <summary>
/// 1 ảnh mới upload kèm request (không phải IFormFile — Application layer không phụ thuộc
/// ASP.NET Core; controller unwrap IFormFile thành record này, giống pattern
/// <c>ProductService.UploadImageAsync(Stream, fileName, contentType, ...)</c>).
/// </summary>
public sealed record NewModel3DImage(Stream Content, string FileName, string ContentType);

/// <summary>
/// Tạo yêu cầu sinh/tạo lại model 3D. Nguồn ảnh = <see cref="SourceImageIds"/> (ảnh có sẵn được tick)
/// + <see cref="NewImages"/> (upload mới, sẽ lưu thành <c>ProductImage</c> bình thường). Tổng 1–4 ảnh.
/// Bỏ trống cả 2 khi tạo Initial → dùng ảnh primary (SortOrder nhỏ nhất) của sản phẩm.
/// Dùng chung cho cả bước tạo request (owner/garden staff) lẫn staff sàn generate/retry.
/// </summary>
public class RequestModel3DRequest
{
    /// <summary>Ảnh đại diện mà model kết quả sẽ gắn vào.</summary>
    public Guid? ProductImageId { get; set; }
    public List<Guid>? SourceImageIds { get; set; }
    public List<NewModel3DImage>? NewImages { get; set; }
}

public class RejectModel3DRequestRequest
{
    public string Reason { get; set; } = null!;
}

public class ToggleModel3DVisibilityRequest
{
    public bool IsEnabled { get; set; }
}

/// <summary>Lịch sử request — trả cho garden owner/garden staff. KHÔNG có field lý do lỗi nội bộ.</summary>
public class Model3DRequestResponse
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductImageId { get; set; }

    /// <summary>Initial | Regenerate.</summary>
    public string RequestType { get; set; } = null!;

    /// <summary>Trạng thái đã che giấu lỗi hết credit — owner luôn thấy "Queued"/"Processing"/"AwaitingStaff"/... không bao giờ thấy lý do thật.</summary>
    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>1 item trong hàng chờ của staff sàn — đầy đủ, KÈM lý do lỗi nội bộ + thông tin product/store.</summary>
public class Model3DRequestQueueItemResponse
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string StoreName { get; set; } = null!;
    public Guid? ProductImageId { get; set; }
    public string? ProductImageUrl { get; set; }

    public string RequestType { get; set; } = null!;
    public string Status { get; set; } = null!;

    public List<Guid> SourceImageIds { get; set; } = new();
    public string? MeshyTaskId { get; set; }
    public Guid? AssignedStaffId { get; set; }

    /// <summary>Staff-only: InsufficientCredits | GenerationFailed | InvalidImage.</summary>
    public string? InternalFailureReason { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? RejectedReason { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Model3DRequestQueueResponse
{
    public List<Model3DRequestQueueItemResponse> Items { get; set; } = new();
    public int Total { get; set; }
    public Dictionary<string, int> StatusCounts { get; set; } = new();
}

/// <summary>
/// Xem trước kết quả Meshy hiện tại (live poll trực tiếp, không lưu DB) — dùng URL tạm của Meshy
/// (tự hết hạn theo asset retention) để staff xem trước khi quyết định accept hay retry. Chỉ khi
/// accept mới tải về re-host vĩnh viễn trên Supabase Storage.
/// </summary>
public class Model3DPreviewResponse
{
    /// <summary>Running | Succeeded | Failed.</summary>
    public string State { get; set; } = null!;
    public int Progress { get; set; }
    public string? ThumbnailUrl { get; set; }

    /// <summary>URL GLB tạm của Meshy — chỉ dùng để xem trước, không lưu lâu dài.</summary>
    public string? GlbUrl { get; set; }
    public string? Error { get; set; }
}
