namespace FengDeskAI.Application.Interfaces.External;

/// <summary>
/// Hàng đợi nền cho bot AI trả lời trong phòng chung (Phase 3). LLM chậm → KHÔNG xử lý đồng bộ
/// trong request; đẩy chatboxId vào đây, worker nền xử lý + broadcast realtime.
/// </summary>
public interface IAiBotQueue
{
    void Enqueue(Guid chatboxId);
}
