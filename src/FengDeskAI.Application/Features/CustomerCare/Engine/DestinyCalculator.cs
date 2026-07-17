using FengDeskAI.Domain.Enums;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Domain.Enums.Recommendation;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>Một hướng Bát Trạch kèm tên cung + ý nghĩa (vd Đông Bắc — Sinh Khí — bứt phá năng suất).</summary>
public sealed record BatTrachDirection(string Direction, string CungName, string Meaning);

/// <summary>
/// Hồ sơ mệnh của MỘT người (không nhất thiết là user) — graceful theo dữ liệu có:
/// chỉ năm sinh → can chi/nạp âm/con giáp; thêm giới tính → cung phi + 8 hướng Bát Trạch.
/// </summary>
public sealed record DestinyProfile(
    int LunarYear,
    string CanChiYear,
    string ZodiacAnimal,
    string ZodiacTrait,
    FengShuiElement Element,
    string NapAmName,
    string NapAmMeaning,
    int? KuaNumber,
    string? CungMenh,
    string? CungMenhElement,
    string? KuaGroupName,
    IReadOnlyList<BatTrachDirection>? FavorableDirections,
    IReadOnlyList<BatTrachDirection>? UnfavorableDirections);

/// <summary>
/// Tính Cung Mệnh (Bát Trạch) + bản mệnh nạp âm cho người bất kỳ. Deterministic — nguồn sự thật
/// duy nhất, AI chỉ diễn giải. Data tra cứu nhập từ <c>docs/adr/note.md</c>.
/// </summary>
public static class DestinyCalculator
{
    private static readonly string[] Can =
        { "Giáp", "Ất", "Bính", "Đinh", "Mậu", "Kỷ", "Canh", "Tân", "Nhâm", "Quý" };

    private static readonly string[] Chi =
        { "Tý", "Sửu", "Dần", "Mão", "Thìn", "Tỵ", "Ngọ", "Mùi", "Thân", "Dậu", "Tuất", "Hợi" };

    private static readonly (string Animal, string Trait)[] Zodiac =
    {
        ("Chuột", "nhanh nhẹn, tháo vát, ứng biến giỏi"),
        ("Trâu", "bền bỉ, chăm chỉ, đáng tin cậy"),
        ("Hổ", "mạnh mẽ, quyết đoán, dám nghĩ dám làm"),
        ("Mèo", "khéo léo, ôn hòa, tinh tế"),
        ("Rồng", "tham vọng, khí chất, tầm nhìn lớn"),
        ("Rắn", "thâm trầm, trực giác nhạy, kín đáo"),
        ("Ngựa", "năng động, phóng khoáng, yêu tự do"),
        ("Dê", "hiền hòa, giàu cảm xúc, thiên hướng nghệ thuật"),
        ("Khỉ", "linh hoạt, thông minh, hài hước"),
        ("Gà", "tỉ mỉ, cầu toàn, có tổ chức"),
        ("Chó", "trung thành, trách nhiệm, chính trực"),
        ("Lợn", "hào phóng, chân thành, hưởng thụ cân bằng"),
    };

    /// <summary>Nghĩa 30 nạp âm — key khớp tên trong <see cref="FengShuiCalculator.GetNapAmName"/>.</summary>
    private static readonly Dictionary<string, string> NapAmMeanings = new()
    {
        ["Hải Trung Kim"] = "Vàng dưới biển — tiềm năng lớn còn ẩn sâu",
        ["Lư Trung Hỏa"] = "Lửa trong lò — cháy âm ỉ, cần mẫn, có sức nung chảy",
        ["Đại Lâm Mộc"] = "Gỗ rừng già — cây cổ thụ to lớn, che chở",
        ["Lộ Bàng Thổ"] = "Đất ven đường — nén chặt, vững chãi, rộng lớn",
        ["Kiếm Phong Kim"] = "Vàng mũi kiếm — sắt thép đã tôi luyện, cực kỳ sắc bén",
        ["Sơn Đầu Hỏa"] = "Lửa trên núi — ngọn lửa bốc cao, rực rỡ",
        ["Giản Hạ Thủy"] = "Nước dưới khe — mạch ngầm tĩnh lặng, sâu sắc",
        ["Thành Đầu Thổ"] = "Đất trên thành — cứng cáp, mang tính bảo vệ",
        ["Bạch Lạp Kim"] = "Vàng trong sáp — kim loại đang định hình",
        ["Dương Liễu Mộc"] = "Gỗ cây liễu — mềm mại, uyển chuyển",
        ["Tuyền Trung Thủy"] = "Nước trong suối — chảy liên tục, linh hoạt, thanh khiết, trí tuệ",
        ["Ốc Thượng Thổ"] = "Đất trên mái nhà — che mưa chắn gió",
        ["Tích Lịch Hỏa"] = "Lửa sấm sét — nhanh, bùng nổ, chớp nhoáng",
        ["Tùng Bách Mộc"] = "Gỗ tùng bách — kiên cường bất chấp sương tuyết",
        ["Trường Lưu Thủy"] = "Nước sông dài — dòng chảy cuồn cuộn, vươn xa",
        ["Sa Trung Kim"] = "Vàng trong cát — cần sàng lọc mới tỏa sáng",
        ["Sơn Hạ Hỏa"] = "Lửa dưới chân núi — bếp lửa sinh hoạt, ấm áp",
        ["Bình Địa Mộc"] = "Cây đồng bằng — sinh trưởng vùng đất bằng phẳng",
        ["Bích Thượng Thổ"] = "Đất trên vách tường — cần chỗ dựa để vững",
        ["Kim Bạch Kim"] = "Vàng nguyên chất — tinh khiết",
        ["Phú Đăng Hỏa"] = "Lửa ngọn đèn — soi rọi ban đêm, mang tính trí tuệ",
        ["Thiên Hà Thủy"] = "Nước trên trời — nước mưa gột rửa vạn vật",
        ["Đại Trạch Thổ"] = "Đất bãi đầm — phù sa nuôi dưỡng, linh hoạt",
        ["Thoa Xuyến Kim"] = "Vàng trang sức — đã chế tác thành hình, lấp lánh",
        ["Tang Đố Mộc"] = "Gỗ cây dâu — biểu tượng của sự cống hiến",
        ["Đại Khê Thủy"] = "Nước suối lớn — thác nước, dòng chảy mạnh mẽ",
        ["Sa Trung Thổ"] = "Đất trong cát — tơi xốp, cần bồi đắp liên kết",
        ["Thiên Thượng Hỏa"] = "Lửa trên trời — ánh mặt trời, công minh, rực rỡ nhất",
        ["Thạch Lựu Mộc"] = "Gỗ cây lựu đá — sức sống mãnh liệt nơi khắc nghiệt",
        ["Đại Hải Thủy"] = "Nước biển lớn — bao la, sâu thẳm, mạnh mẽ nhất loài Thủy",
    };

    /// <summary>Kua số → tên cung mệnh (Kua 5 không tồn tại — GetKuaNumber đã đổi nam→2, nữ→8).</summary>
    private static readonly Dictionary<int, string> KuaToCung = new()
    {
        [1] = "Khảm", [2] = "Khôn", [3] = "Chấn", [4] = "Tốn",
        [6] = "Càn", [7] = "Đoài", [8] = "Cấn", [9] = "Ly",
    };

    private static readonly Dictionary<string, string> CungElement = new()
    {
        ["Càn"] = "Kim", ["Đoài"] = "Kim", ["Cấn"] = "Thổ", ["Khôn"] = "Thổ",
        ["Khảm"] = "Thủy", ["Ly"] = "Hỏa", ["Chấn"] = "Mộc", ["Tốn"] = "Mộc",
    };

    // Ý nghĩa 8 cung Bát Trạch (4 cát theo thứ tự tốt dần xuống, 4 hung theo thứ tự xấu dần xuống).
    private static readonly (string Name, string Meaning)[] GoodStars =
    {
        ("Sinh Khí", "thu hút vượng khí — tốt nhất khi cần bứt phá năng suất, khởi đầu dự án mới"),
        ("Diên Niên", "nền tảng ổn định — duy trì tập trung cao độ, bền quan hệ"),
        ("Thiên Y", "sức khỏe, bền bỉ, minh mẫn"),
        ("Phục Vị", "bình an, giữ vững phong độ"),
    };

    private static readonly (string Name, string Meaning)[] BadStars =
    {
        ("Tuyệt Mệnh", "xấu nhất — hao tổn, thất bại lớn; tránh đặt bàn làm việc/giường"),
        ("Ngũ Quỷ", "thị phi, mất mát, quan hệ xấu"),
        ("Lục Sát", "kiện tụng, bất hòa, tình cảm trắc trở"),
        ("Họa Hại", "xui xẻo vặt — nhẹ nhất trong 4 hung"),
    };

    // Bảng hướng theo cung mệnh (docs/adr/note.md):
    // 8 hướng theo thứ tự [Sinh Khí, Diên Niên, Thiên Y, Phục Vị, Tuyệt Mệnh, Ngũ Quỷ, Lục Sát, Họa Hại].
    private static readonly Dictionary<string, string[]> CungDirections = new()
    {
        ["Càn"] = new[] { "Tây", "Tây Nam", "Đông Bắc", "Tây Bắc", "Nam", "Đông", "Bắc", "Đông Nam" },
        ["Đoài"] = new[] { "Tây Bắc", "Đông Bắc", "Tây Nam", "Tây", "Đông", "Nam", "Đông Nam", "Bắc" },
        ["Cấn"] = new[] { "Tây Nam", "Tây", "Tây Bắc", "Đông Bắc", "Đông Nam", "Bắc", "Đông", "Nam" },
        ["Khôn"] = new[] { "Đông Bắc", "Tây Bắc", "Tây", "Tây Nam", "Bắc", "Đông Nam", "Nam", "Đông" },
        ["Khảm"] = new[] { "Đông Nam", "Nam", "Đông", "Bắc", "Tây Nam", "Đông Bắc", "Tây Bắc", "Tây" },
        ["Ly"] = new[] { "Đông", "Bắc", "Đông Nam", "Nam", "Tây Bắc", "Tây", "Tây Nam", "Đông Bắc" },
        ["Chấn"] = new[] { "Nam", "Đông Nam", "Bắc", "Đông", "Tây", "Tây Bắc", "Đông Bắc", "Tây Nam" },
        ["Tốn"] = new[] { "Bắc", "Đông", "Nam", "Đông Nam", "Đông Bắc", "Tây Nam", "Tây", "Tây Bắc" },
    };

    /// <summary>Can chi của một năm (âm lịch) — vd 1984 → "Giáp Tý".</summary>
    public static string GetCanChiYear(int lunarYear)
        => $"{Can[((lunarYear + 6) % 10 + 10) % 10]} {Chi[((lunarYear + 8) % 12 + 12) % 12]}";

    /// <summary>Tính hồ sơ mệnh từ NĂM ÂM LỊCH (đã quy đổi đúng). Gender null/Other → bỏ phần Bát Trạch.</summary>
    public static DestinyProfile Compute(int lunarYear, Gender? gender)
    {
        int chiIdx = ((lunarYear + 8) % 12 + 12) % 12;
        var (animal, trait) = Zodiac[chiIdx];
        var element = FengShuiCalculator.GetNapAmElement(lunarYear);
        var napAm = FengShuiCalculator.GetNapAmName(lunarYear);
        var napAmMeaning = NapAmMeanings.GetValueOrDefault(napAm, "");

        int? kua = null;
        string? cung = null, cungElem = null, groupName = null;
        List<BatTrachDirection>? favorable = null, unfavorable = null;

        if (gender is Gender.Male or Gender.Female)
        {
            kua = FengShuiCalculator.GetKuaNumber(lunarYear, gender.Value);
            cung = KuaToCung[kua.Value];
            cungElem = CungElement[cung];
            groupName = FengShuiCalculator.GetKuaGroup(kua.Value) == KuaGroup.East
                ? "Đông Tứ Trạch" : "Tây Tứ Trạch";

            var dirs = CungDirections[cung];
            favorable = GoodStars.Select((s, i) => new BatTrachDirection(dirs[i], s.Name, s.Meaning)).ToList();
            unfavorable = BadStars.Select((s, i) => new BatTrachDirection(dirs[i + 4], s.Name, s.Meaning)).ToList();
        }

        return new DestinyProfile(
            lunarYear, GetCanChiYear(lunarYear), animal, trait,
            element, napAm, napAmMeaning,
            kua, cung, cungElem, groupName, favorable, unfavorable);
    }

    /// <summary>Tính hồ sơ mệnh từ ngày sinh DƯƠNG lịch — tự đổi âm để lấy đúng năm (người sinh trước Tết).</summary>
    public static DestinyProfile ComputeFromSolar(DateOnly solarDate, Gender? gender)
    {
        var (_, _, lunarYear, _) = LunarCalendarConverter.Solar2Lunar(solarDate.Day, solarDate.Month, solarDate.Year);
        return Compute(lunarYear, gender);
    }
}
