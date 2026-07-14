namespace FengDeskAI.Application.Interfaces.External;

/// <summary>
/// Một job intake workspace: parse mô tả (+ảnh) thành draft. <see cref="OperationId"/> là khóa định tuyến
/// realtime (group SignalR "ai-op-{OperationId}") + khóa cache kết quả để client kết nối lại lấy được.
/// </summary>
public readonly record struct WorkspaceIntakeJob(
    string OperationId, Guid UserId, string? Description, List<string>? ImageUrls, bool? Think);

/// <summary>
/// Hàng đợi nền cho AI intake workspace. LLM chậm (đặc biệt khi kèm ảnh, có thể ~80s) → KHÔNG xử lý đồng bộ
/// trong request (FE timeout); đẩy job vào đây, worker nền chạy rồi push kết quả realtime qua SignalR.
/// </summary>
public interface IWorkspaceIntakeQueue
{
    void Enqueue(WorkspaceIntakeJob job);
}
