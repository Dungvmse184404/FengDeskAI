namespace FengDeskAI.Domain.Enums.Catalog;

/// <summary>
/// Lý do lỗi nội bộ của 1 lần gọi Meshy — CHỈ hiển thị cho staff sàn. API cho garden
/// owner/garden staff không bao giờ trả field này (map thành trạng thái chung "Processing").
/// </summary>
public enum Model3DFailureReason
{
    /// <summary>Meshy trả 402 Payment Required — hết credit. Request vẫn Queued, worker retry theo NextAttemptAt.</summary>
    InsufficientCredits = 0,

    /// <summary>Task Meshy poll về FAILED/CANCELED/EXPIRED (không phải do ảnh) hoặc lỗi HTTP khác.</summary>
    GenerationFailed = 1,

    /// <summary>Meshy từ chối do ảnh không hợp lệ (400 Bad Request, hoặc task_error liên quan input ảnh).</summary>
    InvalidImage = 2,
}
