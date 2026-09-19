namespace FengDeskAI.Domain.Entities.CustomerCare;

/// <summary>
/// Phiên bản công thức chấm điểm đã đóng dấu lên từng phiên gợi ý
/// (<see cref="Recommendation.FormulaVersion"/>).
/// <para>
/// Không dùng enum: giá trị này đi thẳng ra JSON và xuống DB dưới dạng chuỗi, và danh sách chỉ dài
/// thêm theo thời gian chứ không bao giờ bị suy diễn logic — cùng lý do <c>scoring_params.code</c> là
/// chuỗi chứ không phải enum.
/// </para>
/// </summary>
public static class ScoringFormulaVersions
{
    /// <summary><c>gapScore = gap·p / |gap|₁</c> — miền ±0.5; penalty 0.30/0.15/0.20/0.05.</summary>
    public const string V31 = "3.1";

    /// <summary>
    /// <c>gapScore = gap·p / (|gap|₁/2)</c> — miền ±1.0; penalty ×2;
    /// <c>PersonalConflictMode.Scaled</c> (L2). Xem <c>docs/adr/score-explainability-v3.2.md</c>.
    /// </summary>
    public const string V32 = "3.2";

    /// <summary>
    /// Đợt sửa sau v3.2, đủ lớn để không được so điểm trực tiếp với phiên cũ:
    /// <list type="bullet">
    /// <item><b>§12</b> chủ nhân phòng thành một nguồn phiếu trong <c>current</c>.</item>
    /// <item><b>§17</b> nén tương phản <c>current = normalize(m^α)</c>, <c>α</c> seed 0.60.</item>
    /// <item><b>§18</b> phạt phần hành khắc mệnh không trội của vật mang theo người.</item>
    /// <item><b>§19</b> sản phẩm ĐÃ GIAO vào <c>current</c> dùng để chấm điểm, không chỉ vào radar.</item>
    /// <item><b>P5</b> nghề nghiệp bẻ <c>r</c> (tắt sẵn bằng <c>OCCUPATION_SHARE = 0</c>).</item>
    /// </list>
    /// <para>
    /// Phải đánh dấu riêng: không có nó thì phiên lưu trước và sau đợt sửa này cùng mang nhãn "3.2",
    /// và không ai truy được vì sao cùng một sản phẩm × cùng một phòng lại ra hai điểm khác nhau.
    /// </para>
    /// </summary>
    public const string V33 = "3.3";

    /// <summary>
    /// N3 — nghề nghiệp thành trục thứ ba: <c>d = (1−Wp−Wo)·ĝ + Wp·r + Wo·ô</c> (phòng) và
    /// <c>d = (1−Wo)·n̂ + Wo·ô</c> (Carry); N1 (delta bẻ <c>r</c>) gỡ hẳn. Tắt sẵn bằng
    /// <c>OCCUPATION_WEIGHT = 0</c>. Xem <c>docs/adr/occupation-product-fit-v1.md</c>.
    /// </summary>
    public const string V34 = "3.4";

    /// <summary>
    /// v3.5 — trần tổng phiếu tag khi dựng <c>current</c> (<c>TAG_VOTES_CAP = 5</c>): đổi <c>current</c>
    /// ⇒ đổi <c>gap</c> ⇒ đổi điểm, nên là công thức mới. Đi cùng: <c>PERSONAL_WEIGHT_PRIVATE</c> 0.5 → 0.3,
    /// <c>SHARED</c> 0.3 → 0.2 (đổi số, không đổi công thức). Xem <c>docs/adr/current-tag-votes-cap-v3.5.md</c>.
    /// </summary>
    public const string V35 = "3.5";

    /// <summary>
    /// v3.6 — nhánh Carry: dụng thần có <b>kỵ thần</b>; điểm = <c>Σ min(n̂, p) − Σ_{kỵ} p</c> thay cho <c>n̂·p</c>
    /// (khớp hoàn hảo = 100 %, gỡ trần 0.6). Luồng phòng không đổi. Xem <c>docs/adr/personal-need-v3.6.md</c>.
    /// </summary>
    public const string V36 = "3.6";

    /// <summary>Phiên bản mà engine đang chạy — đóng dấu lên mọi phiên gợi ý mới.</summary>
    public const string Current = V36;
}
