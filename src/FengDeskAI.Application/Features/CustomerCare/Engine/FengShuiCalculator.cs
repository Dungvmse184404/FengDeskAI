using FengDeskAI.Domain.Enums;
using FengDeskAI.Domain.Enums.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Tính toán phong thủy thuần (không phụ thuộc DB/infra) — có thể unit test độc lập.
/// Gồm: mệnh Nạp Âm theo năm sinh, Kua number + nhóm hướng theo năm sinh+giới tính,
/// quan hệ ngũ hành giữa hai hành, và hành ứng với mỗi hướng la bàn (Bát quái).
/// </summary>
public static class FengShuiCalculator
{
    // Ngũ hành tương sinh: X sinh ra Generates[X].  Mộc→Hỏa→Thổ→Kim→Thủy→Mộc.
    private static readonly Dictionary<FengShuiElement, FengShuiElement> Generates = new()
    {
        [FengShuiElement.Moc] = FengShuiElement.Hoa,
        [FengShuiElement.Hoa] = FengShuiElement.Tho,
        [FengShuiElement.Tho] = FengShuiElement.Kim,
        [FengShuiElement.Kim] = FengShuiElement.Thuy,
        [FengShuiElement.Thuy] = FengShuiElement.Moc,
    };

    // Ngũ hành tương khắc: X khắc Controls[X].  Mộc→Thổ→Thủy→Hỏa→Kim→Mộc.
    private static readonly Dictionary<FengShuiElement, FengShuiElement> Controls = new()
    {
        [FengShuiElement.Moc] = FengShuiElement.Tho,
        [FengShuiElement.Tho] = FengShuiElement.Thuy,
        [FengShuiElement.Thuy] = FengShuiElement.Hoa,
        [FengShuiElement.Hoa] = FengShuiElement.Kim,
        [FengShuiElement.Kim] = FengShuiElement.Moc,
    };

    // Hành ứng với mỗi hướng theo Bát quái — dùng cho gợi ý vật phẩm hóa giải hướng.
    private static readonly Dictionary<CompassDirection, FengShuiElement> DirectionElements = new()
    {
        [CompassDirection.North] = FengShuiElement.Thuy,     // Khảm
        [CompassDirection.Northeast] = FengShuiElement.Tho,  // Cấn
        [CompassDirection.East] = FengShuiElement.Moc,       // Chấn
        [CompassDirection.Southeast] = FengShuiElement.Moc,  // Tốn
        [CompassDirection.South] = FengShuiElement.Hoa,      // Ly
        [CompassDirection.Southwest] = FengShuiElement.Tho,  // Khôn
        [CompassDirection.West] = FengShuiElement.Kim,       // Đoài
        [CompassDirection.Northwest] = FengShuiElement.Kim,  // Càn
    };

    private static readonly CompassDirection[] EastGroupDirections =
        { CompassDirection.North, CompassDirection.South, CompassDirection.East, CompassDirection.Southeast };

    private static readonly CompassDirection[] WestGroupDirections =
        { CompassDirection.West, CompassDirection.Northwest, CompassDirection.Southwest, CompassDirection.Northeast };

    /// <summary>
    /// Mệnh Nạp Âm từ năm sinh (dương lịch). Thuật toán Can-Chi:
    /// giá trị Can (Giáp/Ất=1…Nhâm/Quý=5) + giá trị Chi (theo nhóm tam hợp), rút gọn ≤5 → ngũ hành.
    /// Lưu ý: dùng năm dương lịch (bỏ qua ranh giới Tết âm) — chấp nhận cho phạm vi capstone.
    /// </summary>
    public static FengShuiElement GetNapAmElement(int birthYear)
    {
        // Can: 1990→Canh; giá trị cặp Can: Giáp/Ất=1, Bính/Đinh=2, Mậu/Kỷ=3, Canh/Tân=4, Nhâm/Quý=5.
        int stem = ((birthYear % 10) + 10) % 10; // 0=Canh,1=Tân,2=Nhâm,3=Quý,4=Giáp,5=Ất,6=Bính,7=Đinh,8=Mậu,9=Kỷ
        int canValue = stem switch
        {
            4 or 5 => 1, // Giáp, Ất
            6 or 7 => 2, // Bính, Đinh
            8 or 9 => 3, // Mậu, Kỷ
            0 or 1 => 4, // Canh, Tân
            _ => 5,      // Nhâm (2), Quý (3)
        };

        // Chi: nhóm tam hợp → Tý/Sửu/Ngọ/Mùi=0, Dần/Mão/Thân/Dậu=1, Thìn/Tỵ/Tuất/Hợi=2.
        int branch = ((birthYear % 12) + 12) % 12; // 0=Thân,1=Dậu,2=Tuất,3=Hợi,4=Tý,5=Sửu,6=Dần,7=Mão,8=Thìn,9=Tỵ,10=Ngọ,11=Mùi
        int chiValue = branch switch
        {
            4 or 5 or 10 or 11 => 0, // Tý, Sửu, Ngọ, Mùi
            6 or 7 or 0 or 1 => 1,   // Dần, Mão, Thân, Dậu
            _ => 2,                  // Thìn, Tỵ, Tuất, Hợi
        };

        int sum = canValue + chiValue;
        if (sum > 5) sum -= 5;

        return sum switch
        {
            1 => FengShuiElement.Kim,
            2 => FengShuiElement.Thuy,
            3 => FengShuiElement.Hoa,
            4 => FengShuiElement.Tho,
            _ => FengShuiElement.Moc, // 5
        };
    }

    /// <summary>
    /// Kua number (1..9, không có 5) theo năm sinh + giới tính. Chỉ hợp lệ cho Nam/Nữ.
    /// </summary>
    public static int GetKuaNumber(int birthYear, Gender gender)
    {
        int lastTwo = birthYear % 100;
        int reduced = ReduceToSingleDigit(lastTwo / 10 + lastTwo % 10);
        bool millennium = birthYear >= 2000;

        if (gender == Gender.Male)
        {
            int kua = (millennium ? 9 : 10) - reduced;
            kua = ((kua % 9) + 9) % 9; // chuẩn hóa về 0..8
            if (kua == 0) kua = 9;
            return kua == 5 ? 2 : kua; // nam Kua 5 → 2 (Khôn)
        }

        // Female
        int kuaF = ReduceToSingleDigit(reduced + (millennium ? 6 : 5));
        if (kuaF == 0) kuaF = 9;
        return kuaF == 5 ? 8 : kuaF; // nữ Kua 5 → 8 (Cấn)
    }

    public static KuaGroup GetKuaGroup(int kuaNumber)
        => kuaNumber is 1 or 3 or 4 or 9 ? KuaGroup.East : KuaGroup.West;

    /// <summary>
    /// Dựng hồ sơ phong thủy cá nhân từ ngày sinh + giới tính — NGUỒN CHÂN LÝ DUY NHẤT cho việc gating:
    /// không có ngày sinh → null (bỏ phần cá nhân); có ngày sinh nhưng giới tính không Nam/Nữ → chỉ có mệnh
    /// (Nạp Âm), không có Kua/hướng. Dùng chung bởi engine chấm điểm và phần hiển thị hồ sơ (get_my_profile).
    /// </summary>
    public static PersonalProfile? BuildPersonalProfile(DateTime? dateOfBirth, Gender gender)
    {
        if (dateOfBirth is null)
            return null;

        int year = dateOfBirth.Value.Year;
        var element = GetNapAmElement(year); // mệnh Nạp Âm: chỉ cần năm sinh

        // Kua + hướng tốt cần giới tính Nam/Nữ.
        if (gender is Gender.Male or Gender.Female)
        {
            int kua = GetKuaNumber(year, gender);
            var group = GetKuaGroup(kua);
            return new PersonalProfile(element, kua, group, GetFavorableDirections(group));
        }

        // Thiếu giới tính → vẫn có mệnh, không có hướng.
        return new PersonalProfile(element, null, null, new HashSet<CompassDirection>());
    }

    public static IReadOnlySet<CompassDirection> GetFavorableDirections(KuaGroup group)
        => (group == KuaGroup.East ? EastGroupDirections : WestGroupDirections).ToHashSet();

    public static FengShuiElement GetDirectionElement(CompassDirection direction)
        => DirectionElements[direction];

    /// <summary>
    /// Quan hệ ngũ hành xét từ mệnh người (<paramref name="subject"/>) tới hành sản phẩm
    /// (<paramref name="obj"/>). Mỗi cặp phân biệt rơi đúng 1 quan hệ.
    /// </summary>
    public static FengShuiRelation GetRelation(FengShuiElement subject, FengShuiElement obj)
    {
        if (subject == obj) return FengShuiRelation.TuongHoa;
        if (Generates[obj] == subject) return FengShuiRelation.TuongSinh;  // obj sinh subject — nuôi dưỡng
        if (Generates[subject] == obj) return FengShuiRelation.TietKhi;    // subject sinh obj — hao tổn
        if (Controls[subject] == obj) return FengShuiRelation.TuongKhac;   // subject khắc obj — chủ động
        return FengShuiRelation.BiKhac;                                    // obj khắc subject — xung khắc
    }

    /// <summary>Điểm mặc định cho mỗi quan hệ (dùng khi seed feng_shui_rules / fallback).</summary>
    public static decimal DefaultScore(FengShuiRelation relation) => relation switch
    {
        FengShuiRelation.TuongHoa => 1.0m,
        FengShuiRelation.TuongSinh => 0.8m,
        FengShuiRelation.TuongKhac => 0.2m,
        FengShuiRelation.TietKhi => -0.2m,
        FengShuiRelation.BiKhac => -1.0m,
        _ => 0m,
    };

    public static IReadOnlyList<FengShuiElement> AllElements { get; } =
        Enum.GetValues<FengShuiElement>();

    private static int ReduceToSingleDigit(int n)
    {
        n = Math.Abs(n);
        while (n > 9) n = n / 10 + n % 10;
        return n;
    }
}
