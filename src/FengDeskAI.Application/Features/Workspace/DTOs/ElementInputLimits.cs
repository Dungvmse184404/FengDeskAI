using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.Workspace.DTOs;

/// <summary>
/// Trần số tag "hiện trạng không gian" mỗi nhóm. Một nguồn duy nhất cho CẢ BA chốt chặn:
/// prompt AI (xin đúng số), normalize sau khi AI trả về (cắt phần dư), và lúc lưu
/// (<c>WorkspaceProfileService.ResolveValidInputsAsync</c> — chốt thật, vì FE sửa được).
///
/// <para>
/// Vì sao phải có trần: mỗi tag là một "phiếu" trong <c>current</c>. Khai càng nhiều tag thì
/// <c>current</c> càng gần phân bố đều, mà phân bố đều thì phủ tốt với mọi <c>adjustedIdeal</c> ⇒
/// gap teo lại và điểm mọi sản phẩm xích về giữa. <c>TAG_VOTES_CAP</c> đã chặn TỔNG phiếu tag, nhưng
/// nó chặn ở mức tổng — vẫn để lọt chuyện một nhóm nuốt trọn hạn mức đó bằng 20 tag vụn.
/// </para>
///
/// <para>
/// Màu chặt hơn (3): một phòng thực tế chỉ có vài màu chủ đạo, và màu là nhóm user hay bấm nhất nên
/// cũng là nhóm dễ phình nhất. Các nhóm còn lại 5.
/// </para>
/// </summary>
public static class ElementInputLimits
{
    /// <summary>Trần mặc định cho Material · Shape · DecorItem.</summary>
    public const int DefaultMaxPerKind = 5;

    /// <summary>Trần riêng cho <see cref="ElementInputKind.Color"/>.</summary>
    public const int ColorMaxPerKind = 3;

    /// <summary>Trần của một nhóm.</summary>
    public static int MaxFor(ElementInputKind kind)
        => kind == ElementInputKind.Color ? ColorMaxPerKind : DefaultMaxPerKind;

    /// <summary>
    /// Cắt danh sách về đúng trần của từng nhóm, GIỮ NGUYÊN thứ tự đầu vào — với kết quả AI thì thứ tự
    /// đó là thứ tự ưu tiên model đưa ra, với thao tác tay thì là thứ tự user bấm.
    /// </summary>
    public static List<T> TrimPerKind<T>(IEnumerable<T> items, Func<T, ElementInputKind> kindOf)
    {
        var used = new Dictionary<ElementInputKind, int>();
        var kept = new List<T>();
        foreach (var item in items)
        {
            var kind = kindOf(item);
            used.TryGetValue(kind, out int count);
            if (count >= MaxFor(kind)) continue;
            used[kind] = count + 1;
            kept.Add(item);
        }
        return kept;
    }
}
