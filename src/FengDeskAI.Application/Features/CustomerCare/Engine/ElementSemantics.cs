using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Bảng ngữ nghĩa tĩnh cho 5 hành: tên tiếng Việt, trait (đặc tính) mặc định + override theo
/// <see cref="WorkPurpose"/>, vật phẩm ví dụ, và câu diễn giải khắc. Dùng bởi
/// <see cref="SpaceInsightBuilder"/> để dựng câu — KHÔNG chứa vòng sinh/khắc (dùng lại
/// <see cref="FengShuiCalculator"/>.Generates/Controls qua các hàm public sẵn có).
/// </summary>
public static class ElementSemantics
{
    private sealed record TraitInfo(string Default, string Items);

    private static readonly Dictionary<FengShuiElement, TraitInfo> Defaults = new()
    {
        [FengShuiElement.Kim] = new("tính kỷ luật, sự sắc bén", "vật phẩm kim loại, chuông gió, thiết bị điện tử"),
        [FengShuiElement.Moc] = new("sự sáng tạo, sinh trưởng", "cây xanh, đồ gỗ, tông xanh lá"),
        [FengShuiElement.Thuy] = new("sự linh hoạt, thông suốt", "bể cá mini, gương kính, vật phẩm màu xanh dương"),
        [FengShuiElement.Hoa] = new("nhiệt huyết, sức bật năng lượng", "đèn ánh sáng ấm, nến, tông đỏ cam"),
        [FengShuiElement.Tho] = new("sự tĩnh lặng, vững chãi", "gốm sứ, đá tự nhiên, tông nâu vàng"),
    };

    private static readonly Dictionary<(WorkPurpose Purpose, FengShuiElement Element), string> TraitOverrides = new()
    {
        [(WorkPurpose.Office, FengShuiElement.Kim)] = "tính kỷ luật, hiệu suất làm việc",
        [(WorkPurpose.Office, FengShuiElement.Tho)] = "sự ổn định, đáng tin cậy",
        [(WorkPurpose.Study, FengShuiElement.Moc)] = "sự sáng tạo, khả năng học hỏi",
        [(WorkPurpose.Study, FengShuiElement.Thuy)] = "dòng chảy tư duy, khả năng tiếp thu",
        [(WorkPurpose.Creative, FengShuiElement.Moc)] = "sức sáng tạo, ý tưởng nảy nở",
        [(WorkPurpose.Creative, FengShuiElement.Hoa)] = "cảm hứng, đam mê",
        [(WorkPurpose.Reading, FengShuiElement.Tho)] = "sự tĩnh lặng, tập trung sâu",
        [(WorkPurpose.Reading, FengShuiElement.Thuy)] = "sự thông suốt, thấm hiểu",
        [(WorkPurpose.Gaming, FengShuiElement.Hoa)] = "phản xạ, sự hưng phấn",
        [(WorkPurpose.Gaming, FengShuiElement.Kim)] = "sự sắc bén, quyết đoán",
        [(WorkPurpose.Cooking, FengShuiElement.Hoa)] = "nhiệt huyết nấu nướng, sức sống gia đình",
        [(WorkPurpose.Cooking, FengShuiElement.Tho)] = "sự no đủ, ấm cúng",
        [(WorkPurpose.Dining, FengShuiElement.Tho)] = "sự sum vầy, no đủ",
        [(WorkPurpose.Dining, FengShuiElement.Hoa)] = "hơi ấm bữa ăn, gắn kết gia đình",
        [(WorkPurpose.Relaxation, FengShuiElement.Thuy)] = "sự thư thái, dòng chảy nghỉ ngơi",
        [(WorkPurpose.Relaxation, FengShuiElement.Moc)] = "sức sống tươi mới, thư giãn",
        [(WorkPurpose.Sleep, FengShuiElement.Thuy)] = "giấc ngủ sâu, tĩnh tâm",
        [(WorkPurpose.Sleep, FengShuiElement.Moc)] = "sự phục hồi, tái tạo năng lượng",
        [(WorkPurpose.Childcare, FengShuiElement.Moc)] = "sự phát triển, an toàn cho trẻ",
        [(WorkPurpose.Exercise, FengShuiElement.Hoa)] = "sức bền, năng lượng vận động",
        [(WorkPurpose.Exercise, FengShuiElement.Kim)] = "sự dẻo dai, kỷ luật tập luyện",
    };

    // X khắc Y — câu diễn giải mẫu. Key = (X, Y); tra bằng FengShuiCalculator.GetControlledElement(X) == Y.
    private static readonly Dictionary<(FengShuiElement X, FengShuiElement Y), string> ControlPhrases = new()
    {
        [(FengShuiElement.Kim, FengShuiElement.Moc)] = "tính kỷ luật/máy móc triệt tiêu sự sáng tạo, sinh trưởng",
        [(FengShuiElement.Moc, FengShuiElement.Tho)] = "sự phát triển ồ ạt làm xói mòn sự tĩnh lặng, vững chãi",
        [(FengShuiElement.Tho, FengShuiElement.Thuy)] = "sự trì trệ, nặng nề chặn dòng chảy linh hoạt của tư duy",
        [(FengShuiElement.Thuy, FengShuiElement.Hoa)] = "sự lạnh lẽo, ẩm thấp dập tắt nhiệt huyết, năng lượng",
        [(FengShuiElement.Hoa, FengShuiElement.Kim)] = "sự nóng nảy, bốc đồng nung chảy tính kỷ luật, sắc bén",
    };

    private static readonly Dictionary<WorkPurpose, string> PurposeNames = new()
    {
        [WorkPurpose.Office] = "làm việc văn phòng",
        [WorkPurpose.Study] = "học tập",
        [WorkPurpose.Creative] = "sáng tạo",
        [WorkPurpose.Reading] = "đọc sách",
        [WorkPurpose.Gaming] = "chơi game",
        [WorkPurpose.Cooking] = "nấu ăn",
        [WorkPurpose.Dining] = "dùng bữa",
        [WorkPurpose.Relaxation] = "thư giãn",
        [WorkPurpose.Sleep] = "ngủ nghỉ",
        [WorkPurpose.Childcare] = "chăm sóc trẻ nhỏ",
        [WorkPurpose.Exercise] = "tập luyện thể thao",
        [WorkPurpose.Mixed] = "đa năng",
        [WorkPurpose.Other] = "khác",
    };

    /// <summary>Tên tiếng Việt có dấu của hành (enum value không dấu).</summary>
    public static string ElementName(FengShuiElement e) => e switch
    {
        FengShuiElement.Kim => "Kim",
        FengShuiElement.Moc => "Mộc",
        FengShuiElement.Thuy => "Thủy",
        FengShuiElement.Hoa => "Hỏa",
        FengShuiElement.Tho => "Thổ",
        _ => e.ToString(),
    };

    /// <summary>Trait của hành — override theo mục đích phòng nếu có, không thì dùng mặc định.</summary>
    public static string Trait(FengShuiElement element, WorkPurpose? purpose)
    {
        if (purpose is { } p && TraitOverrides.TryGetValue((p, element), out var overridden))
            return overridden;
        return Defaults[element].Default;
    }

    /// <summary>Vật phẩm ví dụ của hành — chỉ dùng ở câu action.</summary>
    public static string Items(FengShuiElement element) => Defaults[element].Items;

    /// <summary>Câu diễn giải khắc X→Y (X khắc Y).</summary>
    public static string ControlPhrase(FengShuiElement x, FengShuiElement y) => ControlPhrases[(x, y)];

    public static string PurposeVi(WorkPurpose purpose)
        => PurposeNames.TryGetValue(purpose, out var name) ? name : purpose.ToString();
}
