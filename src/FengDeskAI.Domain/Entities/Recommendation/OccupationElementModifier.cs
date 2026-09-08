using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Domain.Entities.Recommendation;

/// <summary>
/// "Nghề này khiến hành X hợp/kỵ hơn bao nhiêu" — delta CÓ THỂ ÂM, cùng pattern
/// <see cref="WorkPurposeElementModifier"/>.
///
/// <para>
/// Delta cộng vào <b>vector điểm quan hệ <c>r</c></b> chứ không vào <c>personalVector</c>: engine chỉ
/// đọc <c>.Dominant()</c> của <c>personalVector</c> nên bẻ vào đó thì công thức nuốt mất, trừ khi delta
/// đủ lớn để lật đỉnh — mà lật đỉnh là nhảy bậc, không mượt. Xem ADR v3.2 §11.3 (phương án N1).
/// </para>
///
/// <para>
/// ⚠️ Nghề nghiệp <b>không đổi được bản mệnh</b>: hành đang <c>BiKhac</c> vẫn phải ở lại phía âm dù
/// delta có lớn tới đâu. Ràng buộc đó cưỡng chế trong <c>ElementDirection</c>, không phó mặc cho dữ liệu.
/// </para>
/// </summary>
public class OccupationElementModifier : BaseEntity
{
    public Guid OccupationId { get; set; }
    public Occupation Occupation { get; set; } = null!;

    public FengShuiElement Element { get; set; }

    /// <summary>Độ dịch điểm quan hệ của hành (numeric(4,3)), có thể âm.</summary>
    public decimal Delta { get; set; }
}
