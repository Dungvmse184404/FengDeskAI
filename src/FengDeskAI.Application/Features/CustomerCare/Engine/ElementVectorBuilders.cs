using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Tra cứu <c>element_input_map</c>: một tín hiệu (kind, code) → các đóng góp (hành, trọng số).
/// Dựng 1 lần từ toàn bộ bảng map rồi dùng chung cho phòng &amp; sản phẩm. Kèm nhãn tiếng Việt
/// (<c>LabelVi</c>) của từng code để FE/insight diễn giải "nguyên do" bằng chính tag user đã khai.
/// </summary>
public sealed class ElementInputResolver
{
    private readonly Dictionary<(ElementInputKind, string), List<KeyValuePair<FengShuiElement, decimal>>> _map;
    private readonly Dictionary<(ElementInputKind, string), string> _labels;

    public ElementInputResolver(IEnumerable<ElementInputMap> rows)
    {
        _map = new();
        _labels = new();
        foreach (var r in rows)
        {
            var key = (r.InputKind, r.InputCode);
            if (!_map.TryGetValue(key, out var list))
                _map[key] = list = new();
            list.Add(new(r.Element, r.Weight));

            if (!string.IsNullOrWhiteSpace(r.LabelVi) && !_labels.ContainsKey(key))
                _labels[key] = r.LabelVi.Trim();
        }
    }

    public IEnumerable<KeyValuePair<FengShuiElement, decimal>> Resolve(ElementInputKind kind, string code)
        => _map.TryGetValue((kind, code), out var list)
            ? list
            : Enumerable.Empty<KeyValuePair<FengShuiElement, decimal>>();

    public IEnumerable<KeyValuePair<FengShuiElement, decimal>> ResolveMany(
        IEnumerable<(ElementInputKind Kind, string Code)> inputs)
        => inputs.SelectMany(i => Resolve(i.Kind, i.Code));

    /// <summary>Nhãn tiếng Việt của tag; không có thì trả về chính code (FE vẫn hiển thị được).</summary>
    public string Label(ElementInputKind kind, string code)
        => _labels.TryGetValue((kind, code), out var label) ? label : code;
}

/// <summary>Nguồn đóng góp vào vector hiện trạng phòng.</summary>
public enum CurrentSourceKind
{
    /// <summary>Nền phòng theo loại (vector Interior) — luôn có mặt như "kiến thức nền".</summary>
    Interior,
    /// <summary>Tag user khai (màu / vật liệu / hình khối / vật trang trí).</summary>
    Tag,
    /// <summary>Sản phẩm đã mua đặt trong phòng.</summary>
    Product,

    /// <summary>
    /// Chủ nhân căn phòng — bản mệnh của họ cũng là một nguồn ngũ hành trong không gian.
    /// <para>
    /// Xếp cùng nhóm PRIOR với <see cref="Interior"/>, KHÔNG phải bằng chứng: nó suy ra từ ngày sinh
    /// chứ không phải thứ user quan sát được trong phòng. Vì thế nó không được tính vào
    /// <c>EvidenceCount</c>/<c>Confidence</c> — nếu tính, một phòng chưa khai tag nào vẫn báo "độ tin
    /// cậy cao" trong khi mọi con số vẫn là suy đoán.
    /// </para>
    /// </summary>
    Person,
}

/// <summary>
/// Một nguồn đóng góp vào Current: <see cref="Votes"/> phiếu, phân bổ theo <see cref="Vector"/> (Σ=1).
/// Phần của nguồn này trong Current[e] = Vector[e] × Votes / TotalVotes.
/// </summary>
public sealed record CurrentContribution(
    CurrentSourceKind Source,
    string Label,
    decimal Votes,
    ElementVector Vector,
    ElementInputKind? InputKind = null,
    string? InputCode = null,
    Guid? ProductId = null);

/// <summary>Kết quả dựng Current kèm breakdown theo nguồn (để FE vẽ radar "tag nào chiếm bao nhiêu %").</summary>
public sealed record CurrentBreakdown(
    ElementVector Current,
    decimal TotalVotes,
    IReadOnlyList<CurrentContribution> Contributions,
    ElementVector RawMass = default)
{
    /// <summary>
    /// Số bằng chứng THẬT (tag + sản phẩm) — 0 nghĩa là Current hoàn toàn suy ra từ prior.
    /// <para>Loại cả <see cref="CurrentSourceKind.Interior"/> lẫn <see cref="CurrentSourceKind.Person"/>:
    /// hai nguồn đó là suy đoán, không phải quan sát.</para>
    /// </summary>
    public int EvidenceCount => Contributions.Count(c => !IsPrior(c.Source));

    /// <summary>
    /// Phần của một nguồn trong <c>Current[e]</c>, đã tính nén tương phản.
    ///
    /// <para>
    /// Nén là phép biến đổi <b>phi tuyến trên tổng của từng hành</b>, nên không thể quy ngược ra
    /// "nguồn i chiếm <c>Votes_i / TotalVotes</c>" như thời tuyến tính. Phân bổ lại theo đúng tỉ lệ
    /// khối lượng THÔ trong chính hành đó:
    /// </para>
    /// <code>
    /// share_i[e] = Current[e] · (Votes_i · Vector_i[e] / RawMass[e])
    /// </code>
    /// <para>
    /// Hai tính chất đi kèm: <c>Σ_i share_i[e] = Current[e]</c> (radar xếp chồng vẫn khít), và tỉ lệ
    /// <b>giữa các nguồn TRONG một hành</b> giữ nguyên y hệt số thô — nên câu chuyện "prior loãng dần
    /// khi user khai thêm tag" của §12 không bị nén động tới. Nén chỉ ép tương phản GIỮA các hành.
    /// </para>
    /// <para>Ở <c>α = 1</c> rút gọn về đúng <c>Vector_i[e] · Votes_i / TotalVotes</c> của bản cũ.</para>
    /// </summary>
    public decimal ShareOf(CurrentContribution source, FengShuiElement element)
    {
        decimal raw = RawMass[element];
        return raw <= 0m ? 0m : Current[element] * (source.Votes * source.Vector[element] / raw);
    }

    /// <summary>Nguồn suy đoán (nền loại phòng, bản mệnh chủ nhân) — đối lập với bằng chứng user khai.</summary>
    public static bool IsPrior(CurrentSourceKind source)
        => source is CurrentSourceKind.Interior or CurrentSourceKind.Person;
}

/// <summary>
/// Chủ nhân căn phòng như một nguồn ngũ hành: <paramref name="Votes"/> phiếu, phân bổ theo bản mệnh.
/// </summary>
/// <param name="Votes">
/// Số phiếu, cùng đơn vị với tag (1 tag ≈ 1 phiếu) và nền phòng (3 phiếu). Cố ý là SỐ PHIẾU chứ không
/// phải tỉ trọng %, để chủ nhân bị bằng chứng làm loãng giống nền phòng.
/// </param>
public sealed record PersonPresence(string Label, decimal Votes, ElementVector Vector);

/// <summary>Dựng nguồn "chủ nhân phòng" từ hồ sơ user — một chỗ duy nhất, để mọi màn hình dùng chung.</summary>
public static class PersonPresenceBuilder
{
    /// <summary>
    /// <c>null</c> khi user chưa có ngày sinh (không tính được bản mệnh) hoặc scope cho 0 phiếu
    /// (<see cref="WorkspaceScope.Public"/> — không gian chung không có chủ nhân).
    /// </summary>
    public static PersonPresence? Build(
        DateTime? dateOfBirth, WorkspaceScope scope, ScoringParameters p)
    {
        decimal votes = p.PersonPresenceVotesFor(scope, dateOfBirth);
        if (votes <= 0m || dateOfBirth is not { } dob) return null;

        var destiny = FengShuiCalculator.GetNapAmElement(FengShuiCalculator.GetLunarYear(dob));
        var vector = FengShuiCalculator.BuildPersonalVector(dob, p.SelfShare, p.SupportShare, p.ChildShare);
        return new PersonPresence($"Bạn - mệnh {destiny}", votes, vector);
    }
}

/// <summary>Sản phẩm đặt trong phòng, đã quy ra vector + số phiếu (dùng cho breakdown có tên).</summary>
public sealed record ProductContribution(Guid ProductId, string Name, ElementVector Vector, decimal VoteWeight);

/// <summary>PHẦN C.2 — dựng vector phòng: ideal → bẻ theo intent → hiện trạng.</summary>
public static class WorkspaceVectorBuilder
{
    /// <summary>Vector lý tưởng từ các row <c>Source == "Ideal"</c> (chuẩn hóa Σ=1).</summary>
    public static ElementVector BuildIdeal(IEnumerable<WorkspaceTypeElement> typeElements)
        => ElementVector.FromContributions(typeElements
            .Where(e => string.Equals(e.Source, WorkspaceElementSources.Ideal, StringComparison.OrdinalIgnoreCase))
            .Select(e => new KeyValuePair<FengShuiElement, decimal>(e.Element, e.Weight)));

    /// <summary>Vector nền phòng từ các row <c>Source == "Interior"</c> (chuẩn hóa Σ=1; Zero nếu loại phòng chưa seed).</summary>
    public static ElementVector BuildInterior(IEnumerable<WorkspaceTypeElement> typeElements)
        => ElementVector.FromContributions(typeElements
            .Where(e => string.Equals(e.Source, WorkspaceElementSources.Interior, StringComparison.OrdinalIgnoreCase))
            .Select(e => new KeyValuePair<FengShuiElement, decimal>(e.Element, e.Weight)));

    /// <summary>Bẻ ideal theo Intent (delta có thể âm) rồi chuẩn hóa lại.</summary>
    public static ElementVector ApplyIntent(ElementVector ideal, IEnumerable<WorkPurposeElementModifier> modifiers)
    {
        var adjusted = ideal;
        foreach (var m in modifiers)
            adjusted = adjusted.Add(ElementVector.Single(m.Element).Scale(m.Delta));
        return adjusted.Normalize();
    }

    /// <summary>Hiện trạng phòng (không sản phẩm) — xem <see cref="BuildCurrentBreakdown"/>.</summary>
    public static ElementVector BuildCurrent(
        IReadOnlyCollection<WorkspaceProfileInput> inputs,
        ElementInputResolver resolver,
        IEnumerable<WorkspaceTypeElement> interiorFallback)
        => BuildCurrentWithProducts(inputs, resolver, interiorFallback,
            Array.Empty<(ElementVector, decimal)>());

    /// <summary>
    /// Số "phiếu" quy ước của nền phòng (vector Interior) — prior kiểu Dirichlet: nền phòng LUÔN có mặt,
    /// tương đương k tag thật. Tag/sản phẩm user khai cộng thêm vào và dần lấn át nền khi đủ nhiều
    /// (3 tag = 50/50 với nền). Tránh hoàn toàn trường hợp một hành = 0 chỉ vì user khai ít tag.
    /// </summary>
    public const decimal InteriorPriorVotes = 3m;

    /// <summary>Nhãn hiển thị của nguồn nền phòng.</summary>
    public const string InteriorLabel = "Nền phòng theo loại";

    /// <summary>Tương thích ngược — tên cũ của <see cref="InteriorPriorVotes"/>.</summary>
    public const decimal InteriorFallbackVotes = InteriorPriorVotes;

    /// <summary>
    /// Hiện trạng phòng + sản phẩm (không cần tên) — dùng cho engine chấm điểm / preview 1 sản phẩm.
    /// Cùng công thức với <see cref="BuildCurrentBreakdown"/>.
    /// </summary>
    public static ElementVector BuildCurrentWithProducts(
        IReadOnlyCollection<WorkspaceProfileInput> inputs,
        ElementInputResolver resolver,
        IEnumerable<WorkspaceTypeElement> interiorFallback,
        IReadOnlyCollection<(ElementVector Vector, decimal VoteWeight)> productContributions,
        decimal? saturationAlpha = null)
        => BuildCurrentBreakdown(inputs, resolver, interiorFallback,
                productContributions.Select(p => new ProductContribution(Guid.Empty, string.Empty, p.Vector, p.VoteWeight)).ToList(),
                saturationAlpha: saturationAlpha)
            .Current;

    /// <summary>
    /// Hiện trạng phòng = nền phòng (Interior × <see cref="InteriorPriorVotes"/> phiếu)
    /// + mỗi tag user khai (≈ 1 phiếu — Σ weight của code trong element_input_map)
    /// + mỗi sản phẩm đặt vào (vector chuẩn hóa × voteWeight), rồi chuẩn hóa Σ=1.
    /// Trả kèm breakdown từng nguồn để FE hiển thị "tag nào chiếm bao nhiêu %" và insight nêu nguyên do.
    /// Loại phòng chưa seed Interior → nền = phân bố đều 0.2 (vẫn không có hành = 0).
    /// </summary>
    public static CurrentBreakdown BuildCurrentBreakdown(
        IReadOnlyCollection<WorkspaceProfileInput> inputs,
        ElementInputResolver resolver,
        IEnumerable<WorkspaceTypeElement> interiorFallback,
        IReadOnlyCollection<ProductContribution> productContributions,
        PersonPresence? person = null,
        decimal? interiorVotes = null,
        decimal? saturationAlpha = null)
    {
        var contributions = new List<CurrentContribution>();

        // 1) Nền phòng — prior k phiếu.
        var interior = BuildInterior(interiorFallback);
        var interiorLabel = InteriorLabel;
        if (interior.L1() <= 0m)
        {
            interior = new ElementVector(0.2m, 0.2m, 0.2m, 0.2m, 0.2m);
            interiorLabel = "Nền phòng (mặc định)";
        }
        contributions.Add(new CurrentContribution(
            CurrentSourceKind.Interior, interiorLabel, interiorVotes ?? InteriorPriorVotes, interior));

        // 2) Tag user khai — mỗi tag = Σ weight của code (≈ 1 phiếu; admin giảm weight thì phiếu giảm theo — chủ đích).
        foreach (var input in inputs)
        {
            var raw = RawSum(resolver.Resolve(input.InputKind, input.InputCode));
            var votes = raw.L1();
            if (votes <= 0m) continue; // code không có trong map → không phải bằng chứng
            contributions.Add(new CurrentContribution(
                CurrentSourceKind.Tag,
                resolver.Label(input.InputKind, input.InputCode),
                votes,
                raw.Normalize(),
                InputKind: input.InputKind,
                InputCode: input.InputCode));
        }

        // 2b) Chủ nhân phòng — cùng cơ chế phiếu, PRIOR chứ không phải bằng chứng.
        //     Vì là số phiếu cố định (không phải tỉ trọng cố định), ảnh hưởng của chủ nhân LOÃNG DẦN
        //     khi user khai thêm tag — đúng cách nền phòng đang cư xử. Tỉ trọng cố định thì dù khai
        //     20 tag thật, bản mệnh vẫn giữ nguyên phần của nó, ngược triết lý "bằng chứng lấn át
        //     suy đoán" của mô hình.
        if (person is { Votes: > 0m } owner && owner.Vector.L1() > 0m)
        {
            contributions.Add(new CurrentContribution(
                CurrentSourceKind.Person, owner.Label, owner.Votes, owner.Vector.Normalize()));
        }

        // 3) Sản phẩm đặt trong phòng.
        foreach (var p in productContributions)
        {
            if (p.VoteWeight <= 0m) continue;
            var v = p.Vector.Normalize();
            if (v.L1() <= 0m) continue;
            contributions.Add(new CurrentContribution(
                CurrentSourceKind.Product,
                p.Name,
                p.VoteWeight,
                v,
                ProductId: p.ProductId == Guid.Empty ? null : p.ProductId));
        }

        // Cộng THÔ theo phiếu rồi chuẩn hóa 1 lần — giữ đúng tỉ lệ giữa các nguồn.
        var total = ElementVector.Zero;
        decimal totalVotes = 0m;
        foreach (var c in contributions)
        {
            total = total.Add(c.Vector.Scale(c.Votes));
            totalVotes += c.Votes;
        }

        // §17 — nén tương phản. Áp lên TỔNG của từng hành, sau khi đã cộng hết mọi nguồn, chứ không
        // áp lên từng nguồn: "phòng này đậm hành X tới đâu" là thuộc tính của khối lượng cuối cùng,
        // không phải của riêng tag hay riêng nền phòng. Nén một nhóm nguồn rồi cộng với nhóm chưa nén
        // là cộng hai hệ đơn vị khác nhau (phiếu vs phiếu^α), và làm mất luôn tính bất biến tỉ lệ.
        //
        // Đặt TRƯỚC Normalize: nén rồi mới chia tổng, nếu ngược lại thì Σ=1 khiến α gần như vô hiệu.
        var alpha = saturationAlpha ?? 1m;
        return new CurrentBreakdown(total.Pow(alpha).Normalize(), totalVotes, contributions, total);
    }

    /// <summary>Cộng dồn contributions KHÔNG chuẩn hóa (khác <see cref="ElementVector.FromContributions"/>).</summary>
    private static ElementVector RawSum(IEnumerable<KeyValuePair<FengShuiElement, decimal>> contributions)
    {
        var v = ElementVector.Zero;
        foreach (var c in contributions)
            v = v.Add(ElementVector.Single(c.Key).Scale(c.Value));
        return v;
    }
}
/// <summary>Hằng cho cột <c>workspace_type_elements.source</c>.</summary>
public static class WorkspaceElementSources
{
    public const string Ideal = "Ideal";
    public const string Interior = "Interior";
}

/// <summary>PHẦN C.3 — dựng vector sản phẩm với 3 tầng nguồn dữ liệu (fallback dần).</summary>
public static class ProductVectorProvider
{
    /// <summary>
    /// Tầng 1: override thủ công → dùng vector nhập tay.
    /// Tầng 2: có product_element_inputs → chất liệu (MATERIAL_SHARE) + màu/hình (COLOR_SHARE).
    /// Tầng 3: backfill từ product_elements (primary/secondary theo FALLBACK_*).
    /// </summary>
    public static ElementVector Build(
        bool isOverridden,
        ElementVector? overriddenVector,
        IReadOnlyCollection<ProductElementInput> inputs,
        ElementInputResolver resolver,
        IEnumerable<(FengShuiElement Element, bool IsPrimary)> productElements,
        ScoringParameters p)
    {
        // Tầng 1
        if (isOverridden && overriddenVector is { } ov)
            return ov.Normalize();

        // Tầng 1.5 — sản phẩm được gắn "loại vật trang trí" (DecorItem, vd SaltLamp): dùng THẲNG
        // contributions của code đó trong element_input_map → đồng bộ tuyệt đối với tag hiện trạng
        // workspace cùng tên (chỉnh weight 1 chỗ trong seed-data/element-input-map.json là cả hai đổi).
        var decorInputs = inputs.Where(i => i.InputKind == ElementInputKind.DecorItem).ToList();
        if (decorInputs.Count > 0)
        {
            var decorVector = ElementVector.FromContributions(
                resolver.ResolveMany(decorInputs.Select(i => (i.InputKind, i.InputCode))));
            if (decorVector.L1() > 0m) return decorVector.Normalize();
        }

        // Tầng 2
        if (inputs.Count > 0)
        {
            var materialVector = ElementVector.FromContributions(resolver.ResolveMany(
                inputs.Where(i => i.InputKind == ElementInputKind.Material).Select(i => (i.InputKind, i.InputCode))));
            var surfaceVector = ElementVector.FromContributions(resolver.ResolveMany(
                inputs.Where(i => i.InputKind is ElementInputKind.Color or ElementInputKind.Shape)
                      .Select(i => (i.InputKind, i.InputCode))));

            return materialVector.Scale(p.MaterialShare)
                .Add(surfaceVector.Scale(p.ColorShare))
                .Normalize();
        }

        // Tầng 3 — backfill
        var elements = productElements.ToList();
        var primary = elements.Where(e => e.IsPrimary).Select(e => (FengShuiElement?)e.Element).FirstOrDefault();
        var secondary = elements.Where(e => !e.IsPrimary).Select(e => (FengShuiElement?)e.Element).FirstOrDefault();

        if (primary is not { } prim)
            return ElementVector.Zero;

        var contrib = new List<KeyValuePair<FengShuiElement, decimal>>
        {
            new(prim, secondary is null ? 1.0m : p.FallbackPrimary),
        };
        if (secondary is { } sec)
            contrib.Add(new(sec, p.FallbackSecondary));

        return ElementVector.FromContributions(contrib);
    }
}
