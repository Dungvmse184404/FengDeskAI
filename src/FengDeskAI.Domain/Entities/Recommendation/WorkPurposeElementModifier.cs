using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Domain.Entities.Recommendation;

/// <summary>
/// Điều chỉnh vector lý tưởng theo mục đích làm việc (Intent). Delta có thể ÂM.
/// Vd Study/Reading: Thủy +0.10, Kim +0.05. Seed sẵn, admin sửa.
/// </summary>
public class WorkPurposeElementModifier : BaseEntity
{
    public WorkPurpose WorkPurpose { get; set; }
    public FengShuiElement Element { get; set; }

    /// <summary>Độ dịch trọng số hành (numeric(4,3)), có thể âm.</summary>
    public decimal Delta { get; set; }
}
