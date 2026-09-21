using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.ApiTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Nợ của đợt 3 — Công nợ nhà cung cấp: sau khi nền tảng hoàn tiền cho khách, khoản tiền đó được
/// ghi nợ về phía vườn để trừ vào kỳ đối soát sau. Vườn có 7 ngày để phản đối; quản lý phán quyết.
///
/// Máy trạng thái đang được khẳng định:
/// <code>
/// Pending  → Disputed  (vườn phản đối, trong hạn)
/// Pending  → Settled   (quản lý chốt giữ khoản trừ)
/// Pending  → Waived    KHÔNG hợp lệ — phải phản đối trước
/// Disputed → Settled | Waived
/// Settled / Waived     → không quay lại được
/// </code>
///
/// Ranh giới phân quyền có một chỗ dễ nhầm: <c>/dispute</c> mở cho policy GardenOwnerOrAbove, tức
/// Admin qua được cửa policy — nhưng service còn đòi đúng chủ vườn của khoản nợ đó, nên Admin
/// không sở hữu vườn vẫn nhận 403.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class VendorLiabilityFlowTests
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public VendorLiabilityFlowTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ===================== Sinh khoản công nợ =====================

    [Fact(DisplayName = "LIAB-01 [Normal] A completed refund creates a pending vendor liability for the store")]
    public async Task CompletedRefund_CreatesPendingLiability()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);

        var response = await Manager().GetAsync($"/api/vendor-liabilities/gardens/{scenario.StoreId}?pageSize=100");
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"{(int)response.StatusCode} {body}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var liability = await FindAsync(response, scenario.LiabilityId);
        Assert.Equal("Pending", liability.GetProperty("status").GetString());
        Assert.Equal(scenario.Amount, liability.GetProperty("amount").GetDecimal());
        Assert.Equal(scenario.TicketId, liability.GetProperty("returnRequestId").GetGuid());
    }

    [Fact(DisplayName = "LIAB-02 [Normal] The dispute deadline is seven days out")]
    public async Task NewLiability_HasSevenDayDisputeWindow()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);

        var response = await Manager().GetAsync($"/api/vendor-liabilities/gardens/{scenario.StoreId}?pageSize=100");
        var deadline = (await FindAsync(response, scenario.LiabilityId)).GetProperty("disputeDeadline").GetDateTime();

        var days = (deadline - DateTime.UtcNow).TotalDays;
        Assert.InRange(days, 6.5, 7.5);
    }

    // ===================== Đọc danh sách =====================

    [Fact(DisplayName = "LIAB-03 [Normal] The garden owner reads their own store's liabilities")]
    public async Task ListLiabilities_AsGardenOwner_Succeeds()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);

        var response = await Owner().GetAsync($"/api/vendor-liabilities/gardens/{scenario.StoreId}?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains((await ApiEnvelope.DataAsync(response)).GetProperty("items").EnumerateArray(),
            l => l.GetProperty("id").GetGuid() == scenario.LiabilityId);
    }

    [Fact(DisplayName = "LIAB-04 [Abnormal] A customer cannot read a store's liabilities")]
    public async Task ListLiabilities_AsCustomer_IsForbidden()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);

        var response = await _fixture.ClientFor(TestRole.Customer)
            .GetAsync($"/api/vendor-liabilities/gardens/{scenario.StoreId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "LIAB-05 [Abnormal] Platform staff cannot read liabilities — the bar is manager")]
    public async Task ListLiabilities_AsStaff_IsForbidden()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);

        var response = await _fixture.ClientFor(TestRole.Staff)
            .GetAsync($"/api/vendor-liabilities/gardens/{scenario.StoreId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "LIAB-06 [Abnormal] A garden owner cannot read another garden's liabilities")]
    public async Task ListLiabilities_ForAnotherGarden_IsForbidden()
    {
        var stranger = await ScenarioUsers.CreateAsync(_fixture, Domain.Enums.UserRole.Customer | Domain.Enums.UserRole.GardenOwner);
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, stranger)
            .GetAsync($"/api/vendor-liabilities/gardens/{scenario.StoreId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ===================== Phản đối =====================

    [Fact(DisplayName = "LIAB-07 [Normal] The garden owner disputes a pending liability")]
    public async Task Dispute_AsOwnerWithinWindow_MovesToDisputed()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);

        var response = await Owner().PostAsJsonAsync($"/api/vendor-liabilities/{scenario.LiabilityId}/dispute",
            new { reason = "Cây rời vườn vẫn khỏe, hư hại do vận chuyển." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal("Disputed", data.GetProperty("status").GetString());
        Assert.Equal("Cây rời vườn vẫn khỏe, hư hại do vận chuyển.", data.GetProperty("disputeReason").GetString());
    }

    [Fact(DisplayName = "LIAB-08 [Abnormal] Disputing without a reason is refused")]
    public async Task Dispute_WithoutReason_IsRejected()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);

        var response = await Owner().PostAsJsonAsync($"/api/vendor-liabilities/{scenario.LiabilityId}/dispute",
            new { reason = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("lý do", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "LIAB-09 [Abnormal] Disputing the same liability twice returns 409")]
    public async Task Dispute_Twice_ReturnsConflict()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);
        await Owner().PostAsJsonAsync($"/api/vendor-liabilities/{scenario.LiabilityId}/dispute",
            new { reason = "Lần một." });

        var second = await Owner().PostAsJsonAsync($"/api/vendor-liabilities/{scenario.LiabilityId}/dispute",
            new { reason = "Lần hai." });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact(DisplayName = "LIAB-10 [Abnormal] Disputing an unknown liability returns 404")]
    public async Task Dispute_UnknownId_ReturnsNotFound()
    {
        var response = await Owner().PostAsJsonAsync($"/api/vendor-liabilities/{Guid.NewGuid()}/dispute",
            new { reason = "Có lý do đàng hoàng." });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "LIAB-11 [Abnormal] An admin who does not own the garden cannot dispute on its behalf")]
    public async Task Dispute_AsAdminNotOwningGarden_IsForbidden()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);

        var response = await Admin().PostAsJsonAsync($"/api/vendor-liabilities/{scenario.LiabilityId}/dispute",
            new { reason = "Admin phản đối hộ." });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "LIAB-12 [Boundary] Disputing after the deadline has passed is refused")]
    public async Task Dispute_AfterDeadline_IsRejected()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);
        await BackdateDeadlineAsync(scenario.LiabilityId);

        var response = await Owner().PostAsJsonAsync($"/api/vendor-liabilities/{scenario.LiabilityId}/dispute",
            new { reason = "Phản đối muộn." });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("quá hạn", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    // ===================== Phán quyết =====================

    [Fact(DisplayName = "LIAB-13 [Normal] The manager rules for the vendor and the liability is waived")]
    public async Task Resolve_VendorWinsAfterDispute_Waives()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);
        await Owner().PostAsJsonAsync($"/api/vendor-liabilities/{scenario.LiabilityId}/dispute",
            new { reason = "Lỗi vận chuyển, không phải lỗi vườn." });

        var response = await Manager().PostAsJsonAsync($"/api/vendor-liabilities/{scenario.LiabilityId}/resolve",
            new { vendorWins = true, note = "Chấp nhận giải trình." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal("Waived", data.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, data.GetProperty("resolvedAt").ValueKind);
    }

    [Fact(DisplayName = "LIAB-14 [Normal] The manager rules against the vendor and the liability is settled")]
    public async Task Resolve_VendorLosesAfterDispute_Settles()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);
        await Owner().PostAsJsonAsync($"/api/vendor-liabilities/{scenario.LiabilityId}/dispute",
            new { reason = "Giải trình không thuyết phục." });

        var response = await Manager().PostAsJsonAsync($"/api/vendor-liabilities/{scenario.LiabilityId}/resolve",
            new { vendorWins = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Settled", (await ApiEnvelope.DataAsync(response)).GetProperty("status").GetString());
    }

    [Fact(DisplayName = "LIAB-15 [Normal] A pending liability can be settled without a prior dispute")]
    public async Task Resolve_PendingVendorLoses_Settles()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);

        var response = await Manager().PostAsJsonAsync($"/api/vendor-liabilities/{scenario.LiabilityId}/resolve",
            new { vendorWins = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Settled", (await ApiEnvelope.DataAsync(response)).GetProperty("status").GetString());
    }

    [Fact(DisplayName = "LIAB-16 [Abnormal] A pending liability cannot be waived without a dispute first")]
    public async Task Resolve_PendingVendorWins_ReturnsConflict()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);

        var response = await Manager().PostAsJsonAsync($"/api/vendor-liabilities/{scenario.LiabilityId}/resolve",
            new { vendorWins = true });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact(DisplayName = "LIAB-17 [Abnormal] A settled liability cannot be ruled on again")]
    public async Task Resolve_AlreadySettled_ReturnsConflict()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);
        await Manager().PostAsJsonAsync($"/api/vendor-liabilities/{scenario.LiabilityId}/resolve", new { vendorWins = false });

        var second = await Manager().PostAsJsonAsync($"/api/vendor-liabilities/{scenario.LiabilityId}/resolve",
            new { vendorWins = false });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact(DisplayName = "LIAB-18 [Abnormal] A settled liability can no longer be disputed")]
    public async Task Dispute_AfterSettle_ReturnsConflict()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);
        await Manager().PostAsJsonAsync($"/api/vendor-liabilities/{scenario.LiabilityId}/resolve", new { vendorWins = false });

        var response = await Owner().PostAsJsonAsync($"/api/vendor-liabilities/{scenario.LiabilityId}/dispute",
            new { reason = "Muốn phản đối sau khi đã chốt." });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact(DisplayName = "LIAB-19 [Abnormal] The garden owner cannot rule on their own liability")]
    public async Task Resolve_AsGardenOwner_IsForbidden()
    {
        var scenario = await RefundedReturnScenario.CreateAsync(_fixture);

        var response = await Owner().PostAsJsonAsync($"/api/vendor-liabilities/{scenario.LiabilityId}/resolve",
            new { vendorWins = true });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "LIAB-20 [Abnormal] Ruling on an unknown liability returns 404")]
    public async Task Resolve_UnknownId_ReturnsNotFound()
    {
        var response = await Manager().PostAsJsonAsync($"/api/vendor-liabilities/{Guid.NewGuid()}/resolve",
            new { vendorWins = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ===================== Helper =====================

    private HttpClient Manager() => _fixture.ClientFor(TestRole.Manager);

    private HttpClient Admin() => _fixture.ClientFor(TestRole.Admin);

    /// <summary>Chủ vườn của cửa hàng do <see cref="SalesScenario"/> dựng chính là user mẫu GardenOwner.</summary>
    private HttpClient Owner() => _fixture.ClientFor(TestRole.GardenOwner);

    private static async Task<JsonElement> FindAsync(HttpResponseMessage response, Guid liabilityId)
    {
        var data = await ApiEnvelope.DataAsync(response);
        return data.GetProperty("items").EnumerateArray().Single(l => l.GetProperty("id").GetGuid() == liabilityId);
    }

    /// <summary>
    /// Đẩy hạn phản đối về quá khứ. <c>DisputeDeadline</c> là thuộc tính duy nhất của
    /// <c>VendorLiability</c> còn setter public — <c>Status</c>, <c>ResolvedBy</c>, <c>ResolvedAt</c>
    /// đều private set và chỉ đổi qua <c>Dispute</c>/<c>Settle</c>/<c>Waive</c>.
    /// </summary>
    private Task BackdateDeadlineAsync(Guid liabilityId)
        => VendorLiabilityMaintenance.BackdateDeadlineAsync(_fixture, liabilityId, DateTime.UtcNow.AddDays(-1));
}
