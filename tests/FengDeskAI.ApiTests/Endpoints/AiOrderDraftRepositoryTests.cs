using FengDeskAI.ApiTests.Infrastructure;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Chat;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Enums.CustomerCare;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Bảng <c>ai_order_drafts</c> trên Postgres thật — những bảo đảm mà unit test (mock repository) không chạm tới:
/// UPDATE có điều kiện chống tạo 2 đơn, unique index "một draft Pending mỗi phòng", xóa theo mốc rewind và
/// dọn draft hết hạn. Không có endpoint riêng (draft chỉ do tool AI ghi) nên gọi thẳng repository qua scope DI.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class AiOrderDraftRepositoryTests
{
    private readonly ApiTestFixture _fixture;

    public AiOrderDraftRepositoryTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "AIDRAFT-01 [Abnormal] Two concurrent confirms claim the same draft - only one wins")]
    public async Task TryClaim_ConcurrentRequests_OnlyOneSucceeds()
    {
        var draft = await SeedDraftAsync();

        var results = await Task.WhenAll(ClaimInOwnScopeAsync(draft.Id), ClaimInOwnScopeAsync(draft.Id));

        Assert.Single(results, claimed => claimed);
        Assert.Equal(AiOrderDraftStatus.Confirming, (await ReloadAsync(draft.Id))!.Status);
    }

    [Fact(DisplayName = "AIDRAFT-02 [Abnormal] A room cannot hold two pending drafts at the same time")]
    public async Task UniqueIndex_SecondPendingDraftInSameRoom_IsRejected()
    {
        var draft = await SeedDraftAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() => InsertAsync(Copy(draft)));
    }

    [Fact(DisplayName = "AIDRAFT-03 [Normal] Releasing a claim returns the draft to pending with the new price")]
    public async Task ReleaseClaimWithNewPrice_RestoresPendingAndUpdatesSnapshot()
    {
        var draft = await SeedDraftAsync();
        await WithDraftsAsync(repo => repo.TryClaimAsync(draft.Id, DateTime.UtcNow));

        await WithDraftsAsync(repo => repo.ReleaseClaimWithNewPriceAsync(draft.Id, 99_000m, 20_000m, 119_000m, DateTime.UtcNow));

        var reloaded = await ReloadAsync(draft.Id);
        Assert.Equal(AiOrderDraftStatus.Pending, reloaded!.Status);
        Assert.Equal(99_000m, reloaded.UnitPriceSnapshot);
        Assert.Equal(119_000m, reloaded.TotalAmountSnapshot);
    }

    [Fact(DisplayName = "AIDRAFT-04 [Normal] Rewinding a conversation removes drafts changed after the rewind point only")]
    public async Task DeletePendingChangedSince_RemovesOnlyNewerDrafts()
    {
        var draft = await SeedDraftAsync();

        var removedBefore = await WithDraftsAsync(repo => repo.DeletePendingChangedSinceAsync(draft.ChatboxId, DateTime.UtcNow.AddMinutes(5)));
        var removedAfter = await WithDraftsAsync(repo => repo.DeletePendingChangedSinceAsync(draft.ChatboxId, DateTime.UtcNow.AddMinutes(-5)));

        Assert.Equal(0, removedBefore);
        Assert.Equal(1, removedAfter);
        Assert.Null(await ReloadAsync(draft.Id));
    }

    [Fact(DisplayName = "AIDRAFT-05 [Boundary] An expired draft is invisible to the assistant and purged by the cleanup sweep")]
    public async Task ExpiredDraft_IsHiddenAndPurged()
    {
        var draft = await SeedDraftAsync(expiresAt: DateTime.UtcNow.AddMinutes(-1));

        var visible = await WithDraftsAsync(repo => repo.GetPendingAsync(draft.UserId, draft.ChatboxId, DateTime.UtcNow));
        var purged = await WithDraftsAsync(repo => repo.PurgeStaleAsync(DateTime.UtcNow, TimeSpan.FromMinutes(5)));

        Assert.Null(visible);
        Assert.True(purged >= 1);
        Assert.Null(await ReloadAsync(draft.Id));
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private async Task<AiOrderDraft> SeedDraftAsync(DateTime? expiresAt = null)
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var sales = await SalesScenario.SeedAsync(_fixture, user.Id, storeCount: 1);
        var chatboxId = Guid.Empty;

        await _fixture.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var chatbox = new Chatbox { IsGroup = false, CreatedByUserId = user.Id, IsAiEnabled = true };
            await db.Set<Chatbox>().AddAsync(chatbox);
            await db.SaveChangesAsync();
            chatboxId = chatbox.Id;
        });

        var store = sales.StoreA;
        var draft = new AiOrderDraft
        {
            UserId = user.Id,
            ChatboxId = chatboxId,
            ProductId = store.ProductId,
            ProductItemId = store.ProductItemId,
            Quantity = 1,
            ShippingAddressId = sales.ShippingAddressId,
            UnitPriceSnapshot = store.Price,
            ShippingFeeSnapshot = 30_000m,
            TotalAmountSnapshot = store.Price + 30_000m,
            ProductName = "Test product",
            AddressText = "Test address",
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddMinutes(60),
        };
        await InsertAsync(draft);
        return draft;
    }

    private static AiOrderDraft Copy(AiOrderDraft d) => new()
    {
        UserId = d.UserId, ChatboxId = d.ChatboxId, ProductId = d.ProductId, ProductItemId = d.ProductItemId,
        Quantity = d.Quantity, ShippingAddressId = d.ShippingAddressId, UnitPriceSnapshot = d.UnitPriceSnapshot,
        ShippingFeeSnapshot = d.ShippingFeeSnapshot, TotalAmountSnapshot = d.TotalAmountSnapshot,
        ProductName = d.ProductName, AddressText = d.AddressText, ExpiresAt = d.ExpiresAt,
    };

    private Task InsertAsync(AiOrderDraft draft) => _fixture.WithScopeAsync(async sp =>
    {
        var uow = sp.GetRequiredService<IUnitOfWork>();
        await uow.AiOrderDrafts.AddAsync(draft);
        await uow.SaveChangesAsync();
    });

    private async Task<bool> ClaimInOwnScopeAsync(Guid draftId)
    {
        // Mỗi lượt một scope (DbContext + connection riêng) — giống 2 request HTTP song song.
        using var scope = _fixture.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().AiOrderDrafts.TryClaimAsync(draftId, DateTime.UtcNow);
    }

    private async Task<T> WithDraftsAsync<T>(Func<IAiOrderDraftRepository, Task<T>> action)
    {
        var result = default(T)!;
        await _fixture.WithScopeAsync(async sp => result = await action(sp.GetRequiredService<IUnitOfWork>().AiOrderDrafts));
        return result;
    }

    private async Task WithDraftsAsync(Func<IAiOrderDraftRepository, Task> action)
        => await _fixture.WithScopeAsync(sp => action(sp.GetRequiredService<IUnitOfWork>().AiOrderDrafts));

    private async Task<AiOrderDraft?> ReloadAsync(Guid draftId)
    {
        AiOrderDraft? result = null;
        await _fixture.WithScopeAsync(async sp =>
            result = await sp.GetRequiredService<AppDbContext>().Set<AiOrderDraft>().AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == draftId));
        return result;
    }
}
