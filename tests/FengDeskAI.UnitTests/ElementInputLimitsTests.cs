using System.Collections.Generic;
using System.Linq;
using FengDeskAI.Application.Features.Workspace.DTOs;
using FengDeskAI.Domain.Enums.Workspace;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// Trần số tag "hiện trạng không gian" mỗi nhóm (<see cref="ElementInputLimits"/>): Color 3, còn lại 5.
///
/// <para>
/// Hợp đồng quan trọng nhất ở đây là <b>KHÔNG BAO GIỜ NÉM LỖI</b>. Ba nơi gọi nó — prompt AI, normalize
/// kết quả AI, và lúc lưu hồ sơ — đều là đường mà một ngoại lệ sẽ làm người dùng mất trắng kết quả vì
/// một chuyện tự xử được. Dư thì cắt, giữ phần ĐẦU (thứ tự AI trả = thứ tự nổi bật nó tự xếp; thao tác
/// tay = thứ tự user bấm), rồi đi tiếp.
/// </para>
/// </summary>
public sealed class ElementInputLimitsTests
{
    private static WorkspaceProfileInputDto Input(ElementInputKind kind, string code) => new(kind, code);

    private static List<WorkspaceProfileInputDto> Many(ElementInputKind kind, int count)
        => Enumerable.Range(1, count).Select(i => Input(kind, $"{kind}{i}")).ToList();

    private static List<WorkspaceProfileInputDto> Trim(IEnumerable<WorkspaceProfileInputDto> items)
        => ElementInputLimits.TrimPerKind(items, i => i.InputKind);

    [Theory(DisplayName = "TAGCAP-01 [Normal] Each kind keeps at most its own limit")]
    [InlineData(ElementInputKind.Color, 3)]
    [InlineData(ElementInputKind.Material, 5)]
    [InlineData(ElementInputKind.DecorItem, 5)]
    [InlineData(ElementInputKind.Shape, 5)]
    public void TrimPerKind_KeepsAtMostTheLimitOfEachKind(ElementInputKind kind, int expected)
    {
        Assert.Equal(expected, ElementInputLimits.MaxFor(kind));
        Assert.Equal(expected, Trim(Many(kind, 20)).Count);
    }

    [Fact(DisplayName = "TAGCAP-02 [Normal] Trimming keeps the first items, in order")]
    public void TrimPerKind_KeepsTheFirstItemsInOrder()
    {
        // "Lấy 5 cái đầu" là hợp đồng, không phải chi tiết cài đặt: AI xếp theo mức nổi bật giảm dần,
        // nên cắt từ đuôi là bỏ đúng những thứ ít quan trọng nhất.
        var trimmed = Trim(Many(ElementInputKind.Material, 9));

        Assert.Equal(
            new[] { "Material1", "Material2", "Material3", "Material4", "Material5" },
            trimmed.Select(i => i.InputCode));
    }

    [Fact(DisplayName = "TAGCAP-03 [Normal] Each kind has its own independent budget")]
    public void TrimPerKind_CountsEachKindSeparately()
    {
        var mixed = Many(ElementInputKind.Color, 6)
            .Concat(Many(ElementInputKind.Material, 6))
            .Concat(Many(ElementInputKind.DecorItem, 1))
            .ToList();

        var trimmed = Trim(mixed);

        Assert.Equal(3, trimmed.Count(i => i.InputKind == ElementInputKind.Color));
        Assert.Equal(5, trimmed.Count(i => i.InputKind == ElementInputKind.Material));
        Assert.Equal(1, trimmed.Count(i => i.InputKind == ElementInputKind.DecorItem));
    }

    [Fact(DisplayName = "TAGCAP-04 [Normal] Interleaved kinds do not steal each other's budget")]
    public void TrimPerKind_HandlesInterleavedKinds()
    {
        // Thứ tự xen kẽ là dạng AI hay trả nhất (nó kể theo mạch câu, không gom nhóm).
        var interleaved = new List<WorkspaceProfileInputDto>();
        for (int i = 1; i <= 6; i++)
        {
            interleaved.Add(Input(ElementInputKind.Color, $"C{i}"));
            interleaved.Add(Input(ElementInputKind.Material, $"M{i}"));
        }

        var trimmed = Trim(interleaved);

        Assert.Equal(new[] { "C1", "C2", "C3" },
            trimmed.Where(i => i.InputKind == ElementInputKind.Color).Select(i => i.InputCode));
        Assert.Equal(new[] { "M1", "M2", "M3", "M4", "M5" },
            trimmed.Where(i => i.InputKind == ElementInputKind.Material).Select(i => i.InputCode));
    }

    [Fact(DisplayName = "TAGCAP-05 [Boundary] Lists at or under the limit pass through untouched")]
    public void TrimPerKind_LeavesShortListsAlone()
    {
        var exact = Many(ElementInputKind.Color, 3).Concat(Many(ElementInputKind.Shape, 5)).ToList();
        Assert.Equal(exact.Select(i => i.InputCode), Trim(exact).Select(i => i.InputCode));

        var under = Many(ElementInputKind.Color, 1);
        Assert.Equal(under.Select(i => i.InputCode), Trim(under).Select(i => i.InputCode));
    }

    [Fact(DisplayName = "TAGCAP-06 [Abnormal] An empty list is not an error")]
    public void TrimPerKind_AcceptsEmptyInput()
    {
        Assert.Empty(Trim(new List<WorkspaceProfileInputDto>()));
    }
}
