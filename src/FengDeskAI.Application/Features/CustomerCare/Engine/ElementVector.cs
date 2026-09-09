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

    /// <summary>
    /// Chia từng thành phần cho <paramref name="k"/>. Chia thẳng chứ KHÔNG nhân <c>1/k</c>: với decimal,
    /// <c>1/0.3</c> đã làm tròn một lần rồi nhân là làm tròn lần hai, lệch so với chia trực tiếp.
    /// </summary>
    public ElementVector Divide(decimal k)
        => new(Tho / k, Kim / k, Thuy / k, Moc / k, Hoa / k);

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

    /// <summary>
    /// Nâng từng trục lên luỹ thừa <paramref name="alpha"/> — <b>nén tương phản</b> theo định luật
    /// luỹ thừa Stevens (dạng hiện đại của Weber–Fechner).
    ///
    /// <para>
    /// Cảm nhận "phòng này nhiều hành X tới đâu" tăng theo <c>khốiLượng^α</c> chứ không tuyến tính:
    /// thêm 2 cây vào phòng đã có 10 cây là chuyện lớn, thêm 2 cây vào phòng đã có 100 cây thì gần
    /// như không ai nhận ra. Với <c>α &lt; 1</c>, mức dịch tương đối bằng <c>α · Δm/m</c> — tỉ lệ với
    /// thay đổi TƯƠNG ĐỐI, đúng trực giác đó.
    /// </para>
    ///
    /// <para>
    /// <c>α = 1</c> <b>thoát sớm, trả về chính vector này</b> — không đi qua <see cref="Math.Pow"/>,
    /// nên kill-switch cho kết quả byte-identical chứ không phải "gần đúng tới chữ số thứ n".
    /// </para>
    ///
    /// <para>
    /// ⚠️ <c>decimal</c> không có luỹ thừa phân số nên phải mượn <c>double</c>. Để engine giữ tính tái
    /// lập, kết quả <b>lượng tử hoá về 9 chữ số thập phân</b>: sai khác 1 ulp của <c>Math.Pow</c> giữa
    /// các nền tảng (~1e-16 tương đối) bị nuốt hoàn toàn, mà độ phân giải vẫn xa hơn mức 3 chữ số API trả.
    /// </para>
    ///
    /// <para>Trục ≤ 0 trả 0: IEEE cho <c>0^0 = 1</c>, sẽ biến một hành KHÔNG có mặt thành có mặt.</para>
    /// </summary>
    public ElementVector Pow(decimal alpha)
    {
        if (alpha == 1m) return this;

        static decimal Axis(decimal value, decimal a)
            => value <= 0m ? 0m : Math.Round((decimal)Math.Pow((double)value, (double)a), 9);

        return new(Axis(Tho, alpha), Axis(Kim, alpha), Axis(Thuy, alpha),
                   Axis(Moc, alpha), Axis(Hoa, alpha));
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
