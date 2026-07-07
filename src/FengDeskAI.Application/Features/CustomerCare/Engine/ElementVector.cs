using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Kiểu dữ liệu lõi engine v3: 5 chỉ số ngũ hành (Thổ, Kim, Thủy, Mộc, Hỏa). Mọi thực thể
/// (Người, Phòng, Sản phẩm) đều biểu diễn bằng vector này. Thuần, unit-test được.
/// Vector <b>trạng thái</b> luôn chuẩn hóa Σ=1; vector <b>Gap</b> là hiệu 2 vector chuẩn hóa
/// (Σ=0, mỗi phần tử ∈ [−1,1], KHÔNG chuẩn hóa lại).
/// </summary>
public readonly record struct ElementVector(
    decimal Tho, decimal Kim, decimal Thuy, decimal Moc, decimal Hoa)
{
    public static ElementVector Zero { get; } = new(0m, 0m, 0m, 0m, 0m);

    /// <summary>Đọc giá trị theo enum hành.</summary>
    public decimal this[FengShuiElement e] => e switch
    {
        FengShuiElement.Tho => Tho,
        FengShuiElement.Kim => Kim,
        FengShuiElement.Thuy => Thuy,
        FengShuiElement.Moc => Moc,
        FengShuiElement.Hoa => Hoa,
        _ => 0m,
    };

    public ElementVector Add(ElementVector o)
        => new(Tho + o.Tho, Kim + o.Kim, Thuy + o.Thuy, Moc + o.Moc, Hoa + o.Hoa);

    public ElementVector Scale(decimal k)
        => new(Tho * k, Kim * k, Thuy * k, Moc * k, Hoa * k);

    /// <summary>Hiệu 2 vector — dùng tính Gap. KHÔNG chuẩn hóa lại.</summary>
    public ElementVector Subtract(ElementVector o)
        => new(Tho - o.Tho, Kim - o.Kim, Thuy - o.Thuy, Moc - o.Moc, Hoa - o.Hoa);

    /// <summary>Clamp âm về 0 rồi chia tổng → Σ = 1. Vector rỗng (tổng 0) trả về Zero.</summary>
    public ElementVector Normalize()
    {
        decimal t = Math.Max(0m, Tho), k = Math.Max(0m, Kim), w = Math.Max(0m, Thuy),
                m = Math.Max(0m, Moc), h = Math.Max(0m, Hoa);
        decimal sum = t + k + w + m + h;
        if (sum == 0m) return Zero;
        return new(t / sum, k / sum, w / sum, m / sum, h / sum);
    }

    /// <summary>Tích vô hướng Σ_e a[e]·b[e].</summary>
    public decimal Dot(ElementVector o)
        => Tho * o.Tho + Kim * o.Kim + Thuy * o.Thuy + Moc * o.Moc + Hoa * o.Hoa;

    /// <summary>Chuẩn L1: Σ_e |a[e]|.</summary>
    public decimal L1()
        => Math.Abs(Tho) + Math.Abs(Kim) + Math.Abs(Thuy) + Math.Abs(Moc) + Math.Abs(Hoa);

    /// <summary>Hành có giá trị lớn nhất (dùng cho quan hệ sinh/khắc &amp; hướng đặt vật phẩm).</summary>
    public FengShuiElement Dominant()
    {
        var best = FengShuiElement.Tho;
        decimal bestVal = Tho;
        void Consider(FengShuiElement e, decimal v) { if (v > bestVal) { bestVal = v; best = e; } }
        Consider(FengShuiElement.Kim, Kim);
        Consider(FengShuiElement.Thuy, Thuy);
        Consider(FengShuiElement.Moc, Moc);
        Consider(FengShuiElement.Hoa, Hoa);
        return best;
    }

    /// <summary>Vector đơn vị {e: 1.0}.</summary>
    public static ElementVector Single(FengShuiElement e) => e switch
    {
        FengShuiElement.Tho => new(1m, 0m, 0m, 0m, 0m),
        FengShuiElement.Kim => new(0m, 1m, 0m, 0m, 0m),
        FengShuiElement.Thuy => new(0m, 0m, 1m, 0m, 0m),
        FengShuiElement.Moc => new(0m, 0m, 0m, 1m, 0m),
        FengShuiElement.Hoa => new(0m, 0m, 0m, 0m, 1m),
        _ => Zero,
    };

    /// <summary>Cộng dồn các đóng góp theo hành rồi chuẩn hóa Σ=1.</summary>
    public static ElementVector FromContributions(IEnumerable<KeyValuePair<FengShuiElement, decimal>> contributions)
    {
        var v = Zero;
        foreach (var c in contributions)
            v = v.Add(Single(c.Key).Scale(c.Value));
        return v.Normalize();
    }

    public IEnumerable<(FengShuiElement Element, decimal Value)> Enumerate()
    {
        yield return (FengShuiElement.Tho, Tho);
        yield return (FengShuiElement.Kim, Kim);
        yield return (FengShuiElement.Thuy, Thuy);
        yield return (FengShuiElement.Moc, Moc);
        yield return (FengShuiElement.Hoa, Hoa);
    }
}
