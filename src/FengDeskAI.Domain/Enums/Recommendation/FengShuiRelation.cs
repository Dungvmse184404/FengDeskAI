namespace FengDeskAI.Domain.Enums.Recommendation;

/// <summary>
/// Quan hệ ngũ hành xét từ <b>mệnh người dùng (subject)</b> tới <b>hành sản phẩm (object)</b>.
/// Mỗi quan hệ ứng với một mức điểm seed trong bảng <c>feng_shui_rules</c>.
/// </summary>
public enum FengShuiRelation
{
    /// <summary>Tỷ hòa — cùng hành (vd Mộc ↔ Mộc). Hợp mệnh.</summary>
    TuongHoa,

    /// <summary>Tương sinh — hành sản phẩm sinh ra mệnh người (vd Thủy sinh Mộc). Nuôi dưỡng.</summary>
    TuongSinh,

    /// <summary>Tiết khí — mệnh người sinh ra hành sản phẩm (vd Mộc sinh Hỏa). Hao tổn nhẹ.</summary>
    TietKhi,

    /// <summary>Tương khắc — mệnh người khắc hành sản phẩm (vd Mộc khắc Thổ). Dùng được, chủ động.</summary>
    TuongKhac,

    /// <summary>Bị khắc — hành sản phẩm khắc mệnh người (vd Kim khắc Mộc). Xung khắc, nên tránh.</summary>
    BiKhac,
}
