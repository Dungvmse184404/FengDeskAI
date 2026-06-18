namespace FengDeskAI.Application.Interfaces.External;

/// <summary>Một job cho bot AI: phòng cần trả lời + người vừa gọi @AI (để scope tool/ngữ cảnh theo họ).</summary>
public readonly record struct AiBotJob(Guid ChatboxId, Guid TriggeredByUserId);

/// <summary>
/// Hàng đợi nền cho bot AI trả lời khi có người gọi @AI trong phòng nhiều người. LLM chậm → KHÔNG xử lý
/// đồng bộ trong request; đẩy job vào đây, worker nền xử lý + broadcast realtime.
/// </summary>
public interface IAiBotQueue
{
    void Enqueue(AiBotJob job);
}
