using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Enums;
using FengDeskAI.Domain.Enums.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// Đợt 4 — engine phong thủy. Đây là lõi nghiệp vụ deterministic: cùng đầu vào luôn ra cùng kết
/// quả, nên khẳng định được GIÁ TRỊ CHÍNH XÁC chứ không chỉ "không nổ" như test API.
///
/// Ca test chia theo Normal / Boundary / Abnormal đúng phân loại N/B/A mà Report5_Unit Test.xls
/// yêu cầu, để lập tài liệu unit test lấy thẳng từ đây.
///
/// Giá trị mong đợi của mệnh Nạp Âm đối chiếu với Lục Thập Hoa Giáp thực tế (vd 1990 Canh Ngọ =
/// Lộ Bàng Thổ), không lấy từ chính code — nếu lấy từ code thì test chỉ chép lại lỗi.
/// </summary>
public sealed class FengShuiCalculatorTests
{
    // ===================== Mệnh Nạp Âm =====================

    [Theory(DisplayName = "ENGINE-NAPAM-01 [Normal] Nap Am element matches the traditional sexagenary cycle")]
    [InlineData(1990, FengShuiElement.Tho)]   // Canh Ngọ  - Lộ Bàng Thổ
    [InlineData(1993, FengShuiElement.Kim)]   // Quý Dậu   - Kiếm Phong Kim
    [InlineData(1995, FengShuiElement.Hoa)]   // Ất Hợi    - Sơn Đầu Hỏa
    [InlineData(1996, FengShuiElement.Thuy)]  // Bính Tý   - Giản Hạ Thủy
    [InlineData(1998, FengShuiElement.Tho)]   // Mậu Dần   - Thành Đầu Thổ
    [InlineData(2000, FengShuiElement.Kim)]   // Canh Thìn - Bạch Lạp Kim
    [InlineData(2003, FengShuiElement.Moc)]   // Quý Mùi   - Dương Liễu Mộc
    public void GetNapAmElement_KnownYears_MatchesTraditionalTable(int birthYear, FengShuiElement expected)
        => Assert.Equal(expected, FengShuiCalculator.GetNapAmElement(birthYear));

    [Fact(DisplayName = "ENGINE-NAPAM-02 [Normal] Nap Am repeats on the 60-year sexagenary cycle")]
    public void GetNapAmElement_RepeatsEverySixtyYears()
    {
        for (var year = 1924; year < 1984; year++)
        {
            Assert.Equal(
                FengShuiCalculator.GetNapAmElement(year),
                FengShuiCalculator.GetNapAmElement(year + 60));
        }
    }

    [Theory(DisplayName = "ENGINE-NAPAM-03 [Boundary] Nap Am is defined at century and millennium boundaries")]
    [InlineData(1900)]
    [InlineData(1999)]
    [InlineData(2000)]
    [InlineData(2099)]
    public void GetNapAmElement_AtBoundaryYears_ReturnsDefinedElement(int birthYear)
        => Assert.Contains(FengShuiCalculator.GetNapAmElement(birthYear), FengShuiCalculator.AllElements);

    [Theory(DisplayName = "ENGINE-NAPAM-04 [Abnormal] Nap Am does not throw on out-of-range or negative years")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-2000)]
    [InlineData(9999)]
    public void GetNapAmElement_OutOfRangeYears_DoesNotThrow(int birthYear)
        => Assert.Contains(FengShuiCalculator.GetNapAmElement(birthYear), FengShuiCalculator.AllElements);

    // ===================== Số Kua =====================

    [Theory(DisplayName = "ENGINE-KUA-01 [Normal] Kua number matches the classic formula for both genders")]
    [InlineData(1990, Gender.Male, 1)]
    [InlineData(1990, Gender.Female, 8)]   // nữ Kua 5 → 8 (Cấn)
    [InlineData(1995, Gender.Male, 2)]     // nam Kua 5 → 2 (Khôn)
    [InlineData(1995, Gender.Female, 1)]
    [InlineData(2000, Gender.Male, 9)]
    [InlineData(2000, Gender.Female, 6)]
    public void GetKuaNumber_KnownInputs_MatchesClassicFormula(int birthYear, Gender gender, int expected)
        => Assert.Equal(expected, FengShuiCalculator.GetKuaNumber(birthYear, gender));

    [Theory(DisplayName = "ENGINE-KUA-02 [Boundary] Kua number always lands in 1..9 and never equals 5")]
    [InlineData(Gender.Male)]
    [InlineData(Gender.Female)]
    public void GetKuaNumber_OverAWideRange_StaysInRangeAndSkipsFive(Gender gender)
    {
        for (var year = 1900; year <= 2100; year++)
        {
            var kua = FengShuiCalculator.GetKuaNumber(year, gender);

            Assert.InRange(kua, 1, 9);
            // Kua 5 không tồn tại trong Bát Trạch: nam quy về 2 (Khôn), nữ quy về 8 (Cấn).
            Assert.NotEqual(5, kua);
        }
    }

    [Theory(DisplayName = "ENGINE-KUA-03 [Normal] Kua group follows the East/West four-house split")]
    [InlineData(1, KuaGroup.East)]
    [InlineData(3, KuaGroup.East)]
    [InlineData(4, KuaGroup.East)]
    [InlineData(9, KuaGroup.East)]
    [InlineData(2, KuaGroup.West)]
    [InlineData(6, KuaGroup.West)]
    [InlineData(7, KuaGroup.West)]
    [InlineData(8, KuaGroup.West)]
    public void GetKuaGroup_ForEachKuaNumber_MatchesTheFourHouseSplit(int kua, KuaGroup expected)
        => Assert.Equal(expected, FengShuiCalculator.GetKuaGroup(kua));

    [Theory(DisplayName = "ENGINE-KUA-04 [Normal] Each Kua group has exactly four favourable directions")]
    [InlineData(KuaGroup.East)]
    [InlineData(KuaGroup.West)]
    public void GetFavorableDirections_ForEachGroup_ReturnsFourDistinctDirections(KuaGroup group)
        => Assert.Equal(4, FengShuiCalculator.GetFavorableDirections(group).Count);

    [Fact(DisplayName = "ENGINE-KUA-05 [Normal] East and West favourable directions never overlap")]
    public void GetFavorableDirections_EastAndWest_DoNotOverlap()
    {
        var east = FengShuiCalculator.GetFavorableDirections(KuaGroup.East);
        var west = FengShuiCalculator.GetFavorableDirections(KuaGroup.West);

        Assert.Empty(east.Intersect(west));
    }

    // ===================== Hồ sơ cá nhân (cổng dữ liệu) =====================

    [Fact(DisplayName = "ENGINE-PROFILE-01 [Abnormal] No date of birth yields no personal profile")]
    public void BuildPersonalProfile_WithoutDateOfBirth_ReturnsNull()
        => Assert.Null(FengShuiCalculator.BuildPersonalProfile(null, Gender.Male));

    [Fact(DisplayName = "ENGINE-PROFILE-02 [Normal] Date of birth plus gender yields element, Kua and directions")]
    public void BuildPersonalProfile_WithDateOfBirthAndGender_ReturnsFullProfile()
    {
        var profile = FengShuiCalculator.BuildPersonalProfile(new DateTime(1990, 6, 15), Gender.Male);

        Assert.NotNull(profile);
        Assert.Equal(FengShuiElement.Tho, profile!.Element);
        Assert.Equal(1, profile.KuaNumber);
        Assert.Equal(KuaGroup.East, profile.Group);
        Assert.Equal(4, profile.FavorableDirections.Count);
    }

    [Theory(DisplayName = "ENGINE-PROFILE-03 [Boundary] Without a male/female gender the profile keeps the element but drops Kua")]
    [InlineData(Gender.Unspecified)]
    [InlineData(Gender.Other)]
    public void BuildPersonalProfile_WithoutBinaryGender_KeepsElementButDropsKua(Gender gender)
    {
        var profile = FengShuiCalculator.BuildPersonalProfile(new DateTime(1990, 6, 15), gender);

        Assert.NotNull(profile);
        Assert.Equal(FengShuiElement.Tho, profile!.Element);
        Assert.Null(profile.KuaNumber);
        Assert.Null(profile.Group);
        Assert.Empty(profile.FavorableDirections);
    }

    [Fact(DisplayName = "ENGINE-PROFILE-04 [Boundary] A birth date before Tet belongs to the previous lunar year")]
    public void BuildPersonalProfile_BeforeLunarNewYear_UsesPreviousLunarYear()
    {
        // Tết 1990 rơi vào 27/01/1990 dương lịch. Sinh 05/01/1990 vẫn thuộc năm âm Kỷ Tỵ (1989),
        // nên mệnh phải theo 1989 chứ không phải 1990 — đây là lý do engine đổi lịch thay vì lấy
        // thẳng năm dương.
        var beforeTet = FengShuiCalculator.BuildPersonalProfile(new DateTime(1990, 1, 5), Gender.Male);
        var afterTet = FengShuiCalculator.BuildPersonalProfile(new DateTime(1990, 6, 15), Gender.Male);

        Assert.NotNull(beforeTet);
        Assert.Equal(FengShuiCalculator.GetNapAmElement(1989), beforeTet!.Element);
        Assert.NotEqual(afterTet!.Element, beforeTet.Element);
    }

    [Fact(DisplayName = "ENGINE-PROFILE-05 [Boundary] The personal vector and the profile agree on the destiny element")]
    public void BuildPersonalVector_BeforeLunarNewYear_AgreesWithBuildPersonalProfile()
    {
        // Regression: BuildPersonalVector từng nhận năm DƯƠNG thô (dob.Year) trong khi
        // BuildPersonalProfile đổi sang năm ÂM — cùng một người ra hai bản mệnh khác nhau,
        // tức mệnh hiển thị lệch với mệnh dùng chấm điểm. Xem ADR personalized-recommendation-v3.1 §7.1.
        var birth = new DateTime(1990, 1, 5); // trước Tết 1990 (27/01) → thuộc năm âm 1989

        var profile = FengShuiCalculator.BuildPersonalProfile(birth, Gender.Male);
        var vector = FengShuiCalculator.BuildPersonalVector(birth, 0.60m, 0.30m, 0.10m);

        Assert.NotNull(profile);
        Assert.Equal(1989, FengShuiCalculator.GetLunarYear(birth));
        Assert.Equal(profile!.Element, vector.Dominant());
    }

    [Fact(DisplayName = "ENGINE-PROFILE-06 [Normal] A birth date after Tet keeps the solar year")]
    public void GetLunarYear_AfterLunarNewYear_MatchesTheSolarYear()
        => Assert.Equal(1990, FengShuiCalculator.GetLunarYear(new DateTime(1990, 6, 15)));

    // ===================== Quan hệ ngũ hành =====================

    [Theory(DisplayName = "ENGINE-REL-01 [Normal] The five-element relation matrix follows the generating and controlling cycles")]
    // Tỷ hòa — cùng hành
    [InlineData(FengShuiElement.Moc, FengShuiElement.Moc, FengShuiRelation.TuongHoa)]
    // Tương sinh — đối tượng SINH ra chủ thể (Thủy sinh Mộc)
    [InlineData(FengShuiElement.Moc, FengShuiElement.Thuy, FengShuiRelation.TuongSinh)]
    // Tiết khí — chủ thể sinh ra đối tượng (Mộc sinh Hỏa)
    [InlineData(FengShuiElement.Moc, FengShuiElement.Hoa, FengShuiRelation.TietKhi)]
    // Tương khắc — chủ thể khắc đối tượng (Mộc khắc Thổ)
    [InlineData(FengShuiElement.Moc, FengShuiElement.Tho, FengShuiRelation.TuongKhac)]
    // Bị khắc — đối tượng khắc chủ thể (Kim khắc Mộc)
    [InlineData(FengShuiElement.Moc, FengShuiElement.Kim, FengShuiRelation.BiKhac)]
    public void GetRelation_ForEachRelationType_MatchesTheClassicCycles(
        FengShuiElement subject, FengShuiElement obj, FengShuiRelation expected)
        => Assert.Equal(expected, FengShuiCalculator.GetRelation(subject, obj));

    [Fact(DisplayName = "ENGINE-REL-02 [Normal] Every element pair maps to exactly one relation")]
    public void GetRelation_ForEveryPair_IsTotalAndDeterministic()
    {
        foreach (var subject in FengShuiCalculator.AllElements)
        {
            foreach (var obj in FengShuiCalculator.AllElements)
            {
                var first = FengShuiCalculator.GetRelation(subject, obj);
                var second = FengShuiCalculator.GetRelation(subject, obj);

                Assert.Equal(first, second);
                Assert.True(Enum.IsDefined(first), $"{subject} vs {obj} cho ra quan hệ không xác định.");
            }
        }
    }

    [Fact(DisplayName = "ENGINE-REL-03 [Normal] Nourishing relations score higher than draining ones")]
    public void DefaultScore_RanksNourishingAboveDraining()
    {
        var same = FengShuiCalculator.DefaultScore(FengShuiRelation.TuongHoa);
        var nourishing = FengShuiCalculator.DefaultScore(FengShuiRelation.TuongSinh);
        var controlling = FengShuiCalculator.DefaultScore(FengShuiRelation.TuongKhac);
        var draining = FengShuiCalculator.DefaultScore(FengShuiRelation.TietKhi);
        var conflicting = FengShuiCalculator.DefaultScore(FengShuiRelation.BiKhac);

        // Thứ tự này quyết định trực tiếp thứ hạng sản phẩm — đảo thứ tự là đảo cả kết quả gợi ý.
        Assert.True(same > nourishing);
        Assert.True(nourishing > controlling);
        Assert.True(controlling > draining);
        Assert.True(draining > conflicting);
    }

    [Fact(DisplayName = "ENGINE-REL-04 [Normal] Generating and controlling cycles are consistent in both directions")]
    public void GeneratingAndControllingCycles_AreConsistentBothWays()
    {
        foreach (var element in FengShuiCalculator.AllElements)
        {
            var child = FengShuiCalculator.GetGeneratedElement(element);
            Assert.Equal(element, FengShuiCalculator.GetGeneratingElement(child));

            // Mỗi hành khắc đúng một hành khác, và không bao giờ khắc chính nó.
            Assert.NotEqual(element, FengShuiCalculator.GetControlledElement(element));
        }
    }
}
