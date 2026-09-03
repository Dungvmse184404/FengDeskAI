using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.ApiTests.Infrastructure;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Sổ địa chỉ của khách và các bảng tra cứu dùng chung (hành / phong cách / vibe).
///
/// Hai quy ước được khẳng định:
/// <list type="bullet">
/// <item>Địa chỉ ĐẦU TIÊN luôn thành mặc định, kể cả khi gửi <c>isDefault: false</c> — nếu không
///   khách sẽ không đặt hàng được vì không có địa chỉ giao.</item>
/// <item>Địa chỉ của người khác trả <b>404 chứ không phải 403</b>: repository lọc luôn theo
///   <c>UserId</c>, nên "không phải của bạn" và "không tồn tại" là một.</item>
/// </list>
///
/// Bảng tra cứu: đọc thì công khai, ghi thì Manager trở lên — nghĩa là <b>Staff bị 403</b> dù thường
/// được coi là "người của nền tảng". Đó chính là ca đáng kiểm ở đây.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class AddressAndTaxonomyFlowTests
{
    private const string ValidPhone = "0901234567";

    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public AddressAndTaxonomyFlowTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ===================== Sổ địa chỉ =====================

    [Fact(DisplayName = "ADDR-01 [Normal] A customer adds a shipping address")]
    public async Task CreateAddress_AsCustomer_Succeeds()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var wardId = await AnyWardIdAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/addresses", new
        {
            wardId,
            streetAddress = "88 Đường Người Nhận",
            recipientName = "Khách kiểm thử",
            recipientPhone = ValidPhone,
            label = "Nhà",
        });

        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"{(int)response.StatusCode} {body}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(user.Id, (await ApiEnvelope.DataAsync(response)).GetProperty("userId").GetGuid());
    }

    [Fact(DisplayName = "ADDR-02 [Normal] The very first address becomes the default even when not asked for")]
    public async Task CreateAddress_FirstOne_BecomesDefault()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await CreateAddressAsync(user, isDefault: false);

        Assert.True((await ApiEnvelope.DataAsync(response)).GetProperty("isDefault").GetBoolean(),
            "Địa chỉ đầu tiên phải tự thành mặc định, nếu không khách không đặt hàng được.");
    }

    [Fact(DisplayName = "ADDR-03 [Normal] Setting a second address as default unsets the first")]
    public async Task SetDefaultAddress_SecondOne_UnsetsTheFirst()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var client = ScenarioUsers.ClientFor(_fixture, user);

        var firstId = (await ApiEnvelope.DataAsync(await CreateAddressAsync(user))).GetProperty("id").GetGuid();
        var secondId = (await ApiEnvelope.DataAsync(await CreateAddressAsync(user))).GetProperty("id").GetGuid();

        var setDefault = await client.PatchAsync($"/api/addresses/{secondId}/set-default", null);
        Assert.Equal(HttpStatusCode.OK, setDefault.StatusCode);

        var list = await client.GetAsync("/api/addresses");
        var addresses = (await ApiEnvelope.DataAsync(list)).EnumerateArray().ToList();

        Assert.True(addresses.Single(a => a.GetProperty("id").GetGuid() == secondId).GetProperty("isDefault").GetBoolean());
        Assert.False(addresses.Single(a => a.GetProperty("id").GetGuid() == firstId).GetProperty("isDefault").GetBoolean());
    }

    [Fact(DisplayName = "ADDR-04 [Abnormal] An address without a recipient name is rejected")]
    public async Task CreateAddress_BlankRecipientName_IsRejected()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var wardId = await AnyWardIdAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/addresses", new
        {
            wardId,
            streetAddress = "88 Đường Người Nhận",
            recipientName = "   ",
            recipientPhone = ValidPhone,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("người nhận", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "ADDR-05 [Abnormal] An address without a street is rejected")]
    public async Task CreateAddress_BlankStreet_IsRejected()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var wardId = await AnyWardIdAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/addresses", new
        {
            wardId,
            streetAddress = "  ",
            recipientName = "Khách kiểm thử",
            recipientPhone = ValidPhone,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "ADDR-06 [Abnormal] An address with an unknown ward is rejected")]
    public async Task CreateAddress_UnknownWard_IsRejected()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/addresses", new
        {
            wardId = Guid.NewGuid(),
            streetAddress = "88 Đường Người Nhận",
            recipientName = "Khách kiểm thử",
            recipientPhone = ValidPhone,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("phường", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "ADDR-07 [Normal] Updating an address persists the change")]
    public async Task UpdateAddress_AsOwner_PersistsChange()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var addressId = (await ApiEnvelope.DataAsync(await CreateAddressAsync(user))).GetProperty("id").GetGuid();
        var wardId = await AnyWardIdAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, user).PutAsJsonAsync($"/api/addresses/{addressId}", new
        {
            wardId,
            streetAddress = "99 Đường Đã Đổi",
            recipientName = "Người nhận mới",
            recipientPhone = ValidPhone,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("99 Đường Đã Đổi", (await ApiEnvelope.DataAsync(response)).GetProperty("streetAddress").GetString());
    }

    [Fact(DisplayName = "ADDR-08 [Abnormal] Another user's address is invisible — 404, not 403")]
    public async Task GetAddress_OfAnotherUser_ReturnsNotFound()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var addressId = (await ApiEnvelope.DataAsync(await CreateAddressAsync(user))).GetProperty("id").GetGuid();
        var stranger = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, stranger).GetAsync($"/api/addresses/{addressId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "ADDR-09 [Normal] A deleted address disappears from the list")]
    public async Task DeleteAddress_AsOwner_RemovesFromList()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var client = ScenarioUsers.ClientFor(_fixture, user);
        var addressId = (await ApiEnvelope.DataAsync(await CreateAddressAsync(user))).GetProperty("id").GetGuid();

        var delete = await client.DeleteAsync($"/api/addresses/{addressId}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var list = await client.GetAsync("/api/addresses");
        Assert.DoesNotContain((await ApiEnvelope.DataAsync(list)).EnumerateArray(),
            a => a.GetProperty("id").GetGuid() == addressId);
    }

    [Fact(DisplayName = "ADDR-10 [Abnormal] Deleting an unknown address returns 404")]
    public async Task DeleteAddress_UnknownId_ReturnsNotFound()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user).DeleteAsync($"/api/addresses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ===================== Bảng tra cứu: hành / phong cách / vibe =====================

    [Fact(DisplayName = "LOOKUP-01 [Normal] The five elements are readable without signing in")]
    public async Task ListElements_Anonymously_ReturnsFiveElements()
    {
        var response = await _fixture.ClientFor(TestRole.Anonymous).GetAsync("/api/elements");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var codes = (await ApiEnvelope.DataAsync(response)).EnumerateArray()
            .Select(e => e.GetProperty("code").GetString()).ToList();

        Assert.Equal(5, codes.Count);
        Assert.Contains("Kim", codes);
        Assert.Contains("Moc", codes);
    }

    [Fact(DisplayName = "LOOKUP-02 [Normal] The seeded styles are readable without signing in")]
    public async Task ListStyles_Anonymously_ContainsSeededCodes()
    {
        var response = await _fixture.ClientFor(TestRole.Anonymous).GetAsync("/api/styles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var codes = (await ApiEnvelope.DataAsync(response)).EnumerateArray()
            .Select(s => s.GetProperty("code").GetString()).ToList();

        Assert.Contains("Modern", codes);
        Assert.Contains("Minimal", codes);
    }

    [Fact(DisplayName = "LOOKUP-03 [Normal] A manager adds a new style and it appears in the public list")]
    public async Task CreateStyle_AsManager_AppearsInList()
    {
        var code = NewCode("STY");

        var create = await Manager().PostAsJsonAsync("/api/styles",
            new { code, name = "Phong cách kiểm thử", sortOrder = 99 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var list = await _fixture.ClientFor(TestRole.Anonymous).GetAsync("/api/styles");
        Assert.Contains((await ApiEnvelope.DataAsync(list)).EnumerateArray(),
            s => s.GetProperty("code").GetString() == code);
    }

    [Fact(DisplayName = "LOOKUP-04 [Abnormal] Adding a style whose code already exists returns 409")]
    public async Task CreateStyle_DuplicateCode_ReturnsConflict()
    {
        var code = NewCode("STY");
        await Manager().PostAsJsonAsync("/api/styles", new { code, name = "Lần một", sortOrder = 1 });

        var second = await Manager().PostAsJsonAsync("/api/styles", new { code, name = "Lần hai", sortOrder = 2 });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact(DisplayName = "LOOKUP-05 [Abnormal] A style with a blank code is rejected")]
    public async Task CreateStyle_BlankCode_IsRejected()
    {
        var response = await Manager().PostAsJsonAsync("/api/styles",
            new { code = "  ", name = "Không có mã", sortOrder = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "LOOKUP-06 [Abnormal] Platform staff cannot add a style — the bar is manager")]
    public async Task CreateStyle_AsStaff_IsForbidden()
    {
        var response = await _fixture.ClientFor(TestRole.Staff)
            .PostAsJsonAsync("/api/styles", new { code = NewCode("STY"), name = "Staff thử", sortOrder = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "LOOKUP-07 [Normal] Deactivating a style hides it from the default listing")]
    public async Task UpdateStyle_SetInactive_HidesFromDefaultListing()
    {
        var code = NewCode("STY");
        await Manager().PostAsJsonAsync("/api/styles", new { code, name = "Sắp tắt", sortOrder = 50 });

        var update = await Manager().PutAsJsonAsync($"/api/styles/{code}",
            new { name = "Đã tắt", isActive = false, sortOrder = 50 });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var active = await _fixture.ClientFor(TestRole.Anonymous).GetAsync("/api/styles");
        Assert.DoesNotContain((await ApiEnvelope.DataAsync(active)).EnumerateArray(),
            s => s.GetProperty("code").GetString() == code);

        var all = await _fixture.ClientFor(TestRole.Anonymous).GetAsync("/api/styles?includeInactive=true");
        Assert.Contains((await ApiEnvelope.DataAsync(all)).EnumerateArray(),
            s => s.GetProperty("code").GetString() == code);
    }

    [Fact(DisplayName = "LOOKUP-08 [Abnormal] Updating a style code that does not exist returns 404")]
    public async Task UpdateStyle_UnknownCode_ReturnsNotFound()
    {
        var response = await Manager().PutAsJsonAsync($"/api/styles/{NewCode("STY")}",
            new { name = "Không tồn tại", isActive = true, sortOrder = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "LOOKUP-09 [Normal] A manager adds a new vibe")]
    public async Task CreateVibe_AsManager_Succeeds()
    {
        var response = await Manager().PostAsJsonAsync("/api/vibes",
            new { code = NewCode("VIB"), name = "Vibe kiểm thử", sortOrder = 99 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(DisplayName = "LOOKUP-10 [Abnormal] A customer cannot add a vibe")]
    public async Task CreateVibe_AsCustomer_IsForbidden()
    {
        var response = await _fixture.ClientFor(TestRole.Customer)
            .PostAsJsonAsync("/api/vibes", new { code = NewCode("VIB"), name = "Khách thử", sortOrder = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "LOOKUP-11 [Normal] The element-input code vocabulary is grouped by kind")]
    public async Task ElementInputCodes_ReturnsGroupsWithCodes()
    {
        var response = await _fixture.ClientFor(TestRole.Customer).GetAsync("/api/catalog/element-input-codes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var groups = (await ApiEnvelope.DataAsync(response)).EnumerateArray().ToList();

        Assert.NotEmpty(groups);
        Assert.Contains(groups, g => g.GetProperty("kind").GetString() == "Color"
                                     && g.GetProperty("codes").GetArrayLength() > 0);
    }

    // ===================== Helper =====================

    private HttpClient Manager() => _fixture.ClientFor(TestRole.Manager);

    /// <summary>Mã duy nhất mỗi lần chạy — bảng tra cứu có unique index trên code.</summary>
    private static string NewCode(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..12];

    private async Task<HttpResponseMessage> CreateAddressAsync(ThrowawayUser user, bool isDefault = false)
    {
        var wardId = await AnyWardIdAsync();
        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/addresses", new
        {
            wardId,
            streetAddress = $"{Random.Shared.Next(1, 999)} Đường Kiểm Thử",
            recipientName = "Khách kiểm thử",
            recipientPhone = ValidPhone,
            isDefault,
            label = "Nhà",
        });

        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "tạo địa chỉ"));
        return response;
    }

    /// <summary>Một phường/xã do <c>GeographySeeder</c> tạo — dữ liệu nền, đọc thẳng DB cho gọn.</summary>
    private async Task<Guid> AnyWardIdAsync()
    {
        var wardId = Guid.Empty;
        await _fixture.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            wardId = await db.Set<Ward>().OrderBy(w => w.Name).Select(w => w.Id).FirstAsync();
        });
        return wardId;
    }
}
