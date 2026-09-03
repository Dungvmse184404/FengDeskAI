using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.ApiTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Đợt 6 — Tham số chấm điểm phong thủy: bộ tham số, bảng ánh xạ input → ngũ hành, hệ số theo mục
/// đích sử dụng, và vector theo loại không gian.
///
/// Đây là **bảng điều khiển của engine chấm điểm**: sai một dòng ở đây là toàn bộ gợi ý sản phẩm
/// lệch theo, nên phần cần khẳng định không chỉ là "lưu được" mà là **ngữ nghĩa upsert**: mọi
/// endpoint PUT ở đây không nhận `id`, mà khớp theo KHÓA TỰ NHIÊN (params theo `code`,
/// element-inputs theo bộ ba `(kind, code, element)`, modifier theo cặp `(purpose, element)`,
/// workspace-type theo bộ ba `(typeId, source, element)`). Gọi hai lần cùng khóa phải SỬA dòng cũ,
/// không được đẻ thêm dòng mới — trùng khóa sẽ đụng unique index và nổ 500.
///
/// Ranh giới phân quyền: policy là ManagerOrAbove, tức **Staff không vào được** dù route nằm dưới
/// <c>/api/admin</c>.
///
/// Không phủ ở đây: giá trị vượt miền cột <c>numeric</c> (ví dụ value ≥ 100) — service không chặn
/// nên EF ném ở lúc lưu và trả 500. Đó là lỗi thật của service, đã báo lại cho nhóm; test không
/// khẳng định 500 để khỏi khóa cứng một hành vi sai.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class ScoringConfigFlowTests
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ScoringConfigFlowTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ===================== Bộ tham số =====================

    [Fact(DisplayName = "SCORE-01 [Normal] The seeded scoring parameters are readable")]
    public async Task GetParams_AfterSeeding_ContainsKnownCodes()
    {
        var response = await Manager().GetAsync("/api/admin/scoring/params");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var codes = (await ApiEnvelope.DataAsync(response)).EnumerateArray()
            .Select(p => p.GetProperty("code").GetString()).ToList();

        Assert.Contains("SELF_SHARE", codes);
        Assert.Contains("SUPPORT_SHARE", codes);
        Assert.Contains("MIN_SCORE_THRESHOLD", codes);
    }

    [Fact(DisplayName = "SCORE-02 [Normal] Updating a parameter overwrites its value")]
    public async Task UpsertParam_ExistingCode_OverwritesValue()
    {
        var code = NewCode("PARAM");
        await Manager().PutAsJsonAsync($"/api/admin/scoring/params/{code}", new { value = 0.4m, description = "Lần một." });

        var response = await Manager().PutAsJsonAsync($"/api/admin/scoring/params/{code}",
            new { value = 0.7m, description = "Lần hai." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal(0.7m, data.GetProperty("value").GetDecimal());
        Assert.Equal("Lần hai.", data.GetProperty("description").GetString());
    }

    [Fact(DisplayName = "SCORE-03 [Normal] Upserting the same parameter twice does not create a duplicate row")]
    public async Task UpsertParam_Twice_KeepsSingleRow()
    {
        var code = NewCode("ONCE");
        await Manager().PutAsJsonAsync($"/api/admin/scoring/params/{code}", new { value = 0.1m });
        await Manager().PutAsJsonAsync($"/api/admin/scoring/params/{code}", new { value = 0.2m });

        var list = await Manager().GetAsync("/api/admin/scoring/params");
        var matching = (await ApiEnvelope.DataAsync(list)).EnumerateArray()
            .Count(p => p.GetProperty("code").GetString() == code);

        Assert.Equal(1, matching);
    }

    [Fact(DisplayName = "SCORE-04 [Normal] A changed parameter is visible on the next read")]
    public async Task UpsertParam_IsVisibleOnNextRead()
    {
        var code = NewCode("READ");
        await Manager().PutAsJsonAsync($"/api/admin/scoring/params/{code}", new { value = 0.33m });

        var list = await Manager().GetAsync("/api/admin/scoring/params");
        var row = (await ApiEnvelope.DataAsync(list)).EnumerateArray()
            .Single(p => p.GetProperty("code").GetString() == code);

        Assert.Equal(0.33m, row.GetProperty("value").GetDecimal());
    }

    [Fact(DisplayName = "SCORE-05 [Abnormal] Platform staff cannot read the scoring parameters")]
    public async Task GetParams_AsStaff_IsForbidden()
    {
        var response = await _fixture.ClientFor(TestRole.Staff).GetAsync("/api/admin/scoring/params");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "SCORE-06 [Abnormal] A customer cannot change a scoring parameter")]
    public async Task UpsertParam_AsCustomer_IsForbidden()
    {
        var response = await _fixture.ClientFor(TestRole.Customer)
            .PutAsJsonAsync("/api/admin/scoring/params/SELF_SHARE", new { value = 0.99m });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ===================== Ánh xạ input → ngũ hành =====================

    [Fact(DisplayName = "SCORE-07 [Normal] The seeded element-input map is readable")]
    public async Task GetElementInputs_AfterSeeding_IsNotEmpty()
    {
        var response = await Manager().GetAsync("/api/admin/scoring/element-inputs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty((await ApiEnvelope.DataAsync(response)).EnumerateArray());
    }

    [Fact(DisplayName = "SCORE-08 [Normal] Creating an element-input mapping stores the weight")]
    public async Task UpsertElementInput_NewTriple_StoresWeight()
    {
        var code = NewCode("COLOR");

        var response = await Manager().PutAsJsonAsync("/api/admin/scoring/element-inputs",
            new { inputKind = "Color", inputCode = code, element = "Hoa", weight = 0.8m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal("Color", data.GetProperty("inputKind").GetString());
        Assert.Equal("Hoa", data.GetProperty("element").GetString());
        Assert.Equal(0.8m, data.GetProperty("weight").GetDecimal());
    }

    [Fact(DisplayName = "SCORE-09 [Normal] Re-sending the same triple updates the row instead of adding one")]
    public async Task UpsertElementInput_SameTriple_UpdatesInPlace()
    {
        var code = NewCode("COLOR");
        var first = await Manager().PutAsJsonAsync("/api/admin/scoring/element-inputs",
            new { inputKind = "Color", inputCode = code, element = "Hoa", weight = 0.5m });
        var firstId = (await ApiEnvelope.DataAsync(first)).GetProperty("id").GetGuid();

        var second = await Manager().PutAsJsonAsync("/api/admin/scoring/element-inputs",
            new { inputKind = "Color", inputCode = code, element = "Hoa", weight = 0.9m });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var data = await ApiEnvelope.DataAsync(second);
        Assert.Equal(firstId, data.GetProperty("id").GetGuid());
        Assert.Equal(0.9m, data.GetProperty("weight").GetDecimal());
    }

    [Fact(DisplayName = "SCORE-10 [Normal] The same input code can map to two different elements")]
    public async Task UpsertElementInput_SameCodeDifferentElement_CreatesSecondRow()
    {
        var code = NewCode("COLOR");
        var first = await Manager().PutAsJsonAsync("/api/admin/scoring/element-inputs",
            new { inputKind = "Color", inputCode = code, element = "Hoa", weight = 0.7m });
        var second = await Manager().PutAsJsonAsync("/api/admin/scoring/element-inputs",
            new { inputKind = "Color", inputCode = code, element = "Tho", weight = 0.3m });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotEqual(
            (await ApiEnvelope.DataAsync(first)).GetProperty("id").GetGuid(),
            (await ApiEnvelope.DataAsync(second)).GetProperty("id").GetGuid());
    }

    [Fact(DisplayName = "SCORE-11 [Boundary] A zero weight on an element-input mapping is rejected")]
    public async Task UpsertElementInput_ZeroWeight_IsRejected()
    {
        var response = await Manager().PutAsJsonAsync("/api/admin/scoring/element-inputs",
            new { inputKind = "Color", inputCode = NewCode("COLOR"), element = "Hoa", weight = 0m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("weight", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "SCORE-12 [Abnormal] A blank input code is rejected")]
    public async Task UpsertElementInput_BlankCode_IsRejected()
    {
        var response = await Manager().PutAsJsonAsync("/api/admin/scoring/element-inputs",
            new { inputKind = "Color", inputCode = "   ", element = "Hoa", weight = 1m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "SCORE-13 [Normal] Deleting an element-input mapping removes it from the list")]
    public async Task DeleteElementInput_ExistingRow_RemovesIt()
    {
        var code = NewCode("COLOR");
        var created = await Manager().PutAsJsonAsync("/api/admin/scoring/element-inputs",
            new { inputKind = "Color", inputCode = code, element = "Hoa", weight = 0.6m });
        var id = (await ApiEnvelope.DataAsync(created)).GetProperty("id").GetGuid();

        var delete = await Manager().DeleteAsync($"/api/admin/scoring/element-inputs/{id}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var list = await Manager().GetAsync("/api/admin/scoring/element-inputs");
        Assert.DoesNotContain((await ApiEnvelope.DataAsync(list)).EnumerateArray(),
            r => r.GetProperty("id").GetGuid() == id);
    }

    [Fact(DisplayName = "SCORE-14 [Abnormal] Deleting an unknown element-input mapping returns 404")]
    public async Task DeleteElementInput_UnknownId_ReturnsNotFound()
    {
        var response = await Manager().DeleteAsync($"/api/admin/scoring/element-inputs/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ===================== Hệ số theo mục đích sử dụng =====================

    [Fact(DisplayName = "SCORE-15 [Normal] The seeded purpose modifiers are readable")]
    public async Task GetPurposeModifiers_AfterSeeding_IsNotEmpty()
    {
        var response = await Manager().GetAsync("/api/admin/scoring/purpose-modifiers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty((await ApiEnvelope.DataAsync(response)).EnumerateArray());
    }

    [Fact(DisplayName = "SCORE-16 [Normal] Upserting a purpose modifier updates the existing pair in place")]
    public async Task UpsertPurposeModifier_SamePair_UpdatesInPlace()
    {
        var first = await Manager().PutAsJsonAsync("/api/admin/scoring/purpose-modifiers",
            new { workPurpose = "Gaming", element = "Hoa", delta = 0.11m });
        var firstId = (await ApiEnvelope.DataAsync(first)).GetProperty("id").GetGuid();

        var second = await Manager().PutAsJsonAsync("/api/admin/scoring/purpose-modifiers",
            new { workPurpose = "Gaming", element = "Hoa", delta = 0.22m });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var data = await ApiEnvelope.DataAsync(second);
        Assert.Equal(firstId, data.GetProperty("id").GetGuid());
        Assert.Equal(0.22m, data.GetProperty("delta").GetDecimal());
    }

    [Fact(DisplayName = "SCORE-17 [Normal] A negative delta is accepted — it discourages an element")]
    public async Task UpsertPurposeModifier_NegativeDelta_IsAccepted()
    {
        var response = await Manager().PutAsJsonAsync("/api/admin/scoring/purpose-modifiers",
            new { workPurpose = "Sleep", element = "Hoa", delta = -0.15m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(-0.15m, (await ApiEnvelope.DataAsync(response)).GetProperty("delta").GetDecimal());
    }

    [Fact(DisplayName = "SCORE-18 [Abnormal] An unknown work purpose is rejected by model binding")]
    public async Task UpsertPurposeModifier_UnknownPurpose_IsRejected()
    {
        var response = await Manager().PutAsJsonAsync("/api/admin/scoring/purpose-modifiers",
            new { workPurpose = "KhongCoMucDichNay", element = "Hoa", delta = 0.1m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "SCORE-19 [Normal] Deleting a purpose modifier removes it")]
    public async Task DeletePurposeModifier_ExistingRow_RemovesIt()
    {
        var created = await Manager().PutAsJsonAsync("/api/admin/scoring/purpose-modifiers",
            new { workPurpose = "Exercise", element = "Kim", delta = 0.07m });
        var id = (await ApiEnvelope.DataAsync(created)).GetProperty("id").GetGuid();

        var delete = await Manager().DeleteAsync($"/api/admin/scoring/purpose-modifiers/{id}");

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
    }

    [Fact(DisplayName = "SCORE-20 [Abnormal] Deleting an unknown purpose modifier returns 404")]
    public async Task DeletePurposeModifier_UnknownId_ReturnsNotFound()
    {
        var response = await Manager().DeleteAsync($"/api/admin/scoring/purpose-modifiers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ===================== Vector theo loại không gian =====================

    [Fact(DisplayName = "SCORE-21 [Normal] The seeded workspace-type vectors are readable")]
    public async Task GetWorkspaceTypeElements_AfterSeeding_IsNotEmpty()
    {
        var response = await Manager().GetAsync("/api/admin/scoring/workspace-type-elements");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty((await ApiEnvelope.DataAsync(response)).EnumerateArray());
    }

    [Fact(DisplayName = "SCORE-22 [Normal] Filtering by workspace type only returns that type's rows")]
    public async Task GetWorkspaceTypeElements_FilteredByType_ReturnsOnlyThatType()
    {
        var typeId = await AnyWorkspaceTypeIdAsync();

        var response = await Manager().GetAsync($"/api/admin/scoring/workspace-type-elements?workspaceTypeId={typeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = (await ApiEnvelope.DataAsync(response)).EnumerateArray().ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal(typeId, r.GetProperty("workspaceTypeId").GetGuid()));
    }

    [Fact(DisplayName = "SCORE-23 [Normal] Upserting a workspace-type vector row updates it in place")]
    public async Task UpsertWorkspaceTypeElement_SameTriple_UpdatesInPlace()
    {
        var typeId = await AnyWorkspaceTypeIdAsync();
        var first = await Manager().PutAsJsonAsync("/api/admin/scoring/workspace-type-elements",
            new { workspaceTypeId = typeId, source = "Ideal", element = "Kim", weight = 0.21m });
        var firstId = (await ApiEnvelope.DataAsync(first)).GetProperty("id").GetGuid();

        var second = await Manager().PutAsJsonAsync("/api/admin/scoring/workspace-type-elements",
            new { workspaceTypeId = typeId, source = "Ideal", element = "Kim", weight = 0.42m });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var data = await ApiEnvelope.DataAsync(second);
        Assert.Equal(firstId, data.GetProperty("id").GetGuid());
        Assert.Equal(0.42m, data.GetProperty("weight").GetDecimal());
    }

    [Fact(DisplayName = "SCORE-24 [Normal] Ideal and Interior are two separate rows for the same element")]
    public async Task UpsertWorkspaceTypeElement_DifferentSource_IsSeparateRow()
    {
        var typeId = await AnyWorkspaceTypeIdAsync();
        var ideal = await Manager().PutAsJsonAsync("/api/admin/scoring/workspace-type-elements",
            new { workspaceTypeId = typeId, source = "Ideal", element = "Thuy", weight = 0.15m });
        var interior = await Manager().PutAsJsonAsync("/api/admin/scoring/workspace-type-elements",
            new { workspaceTypeId = typeId, source = "Interior", element = "Thuy", weight = 0.15m });

        Assert.Equal(HttpStatusCode.OK, interior.StatusCode);
        Assert.NotEqual(
            (await ApiEnvelope.DataAsync(ideal)).GetProperty("id").GetGuid(),
            (await ApiEnvelope.DataAsync(interior)).GetProperty("id").GetGuid());
    }

    [Fact(DisplayName = "SCORE-25 [Abnormal] A source other than Ideal or Interior is rejected")]
    public async Task UpsertWorkspaceTypeElement_UnknownSource_IsRejected()
    {
        var typeId = await AnyWorkspaceTypeIdAsync();

        var response = await Manager().PutAsJsonAsync("/api/admin/scoring/workspace-type-elements",
            new { workspaceTypeId = typeId, source = "Exterior", element = "Kim", weight = 0.2m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Ideal", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "SCORE-26 [Boundary] A negative weight on a workspace-type vector is rejected")]
    public async Task UpsertWorkspaceTypeElement_NegativeWeight_IsRejected()
    {
        var typeId = await AnyWorkspaceTypeIdAsync();

        var response = await Manager().PutAsJsonAsync("/api/admin/scoring/workspace-type-elements",
            new { workspaceTypeId = typeId, source = "Ideal", element = "Kim", weight = -0.1m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "SCORE-27 [Boundary] A zero weight on a workspace-type vector is accepted")]
    public async Task UpsertWorkspaceTypeElement_ZeroWeight_IsAccepted()
    {
        var typeId = await AnyWorkspaceTypeIdAsync();

        var response = await Manager().PutAsJsonAsync("/api/admin/scoring/workspace-type-elements",
            new { workspaceTypeId = typeId, source = "Interior", element = "Hoa", weight = 0m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "SCORE-28 [Abnormal] Deleting an unknown workspace-type vector row returns 404")]
    public async Task DeleteWorkspaceTypeElement_UnknownId_ReturnsNotFound()
    {
        var response = await Manager().DeleteAsync($"/api/admin/scoring/workspace-type-elements/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ===================== Helper =====================

    private HttpClient Manager() => _fixture.ClientFor(TestRole.Manager);

    /// <summary>Mã duy nhất mỗi lần chạy — cột code chỉ 30 ký tự và có unique index.</summary>
    private static string NewCode(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..20].ToUpperInvariant();

    private async Task<Guid> AnyWorkspaceTypeIdAsync()
    {
        var response = await Manager().GetAsync("/api/admin/scoring/workspace-type-elements");
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "đọc vector loại phòng"));

        var rows = (await ApiEnvelope.DataAsync(response)).EnumerateArray().ToList();
        Assert.True(rows.Count > 0,
            "WorkspaceTypeElementSeeder phải chạy trước — không có dòng nào thì mọi ca vector loại phòng vô nghĩa.");

        return rows[0].GetProperty("workspaceTypeId").GetGuid();
    }
}
