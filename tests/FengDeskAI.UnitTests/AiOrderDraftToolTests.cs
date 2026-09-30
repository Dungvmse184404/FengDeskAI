using System.Text.Json;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Application.Features.Catalog.Services;
using FengDeskAI.Application.Features.CustomerCare;
using FengDeskAI.Application.Features.CustomerCare.Tools;
using FengDeskAI.Application.Features.Payment.DTOs;
using FengDeskAI.Application.Features.Payment.Services;
using FengDeskAI.Application.Features.Sales.DTOs;
using FengDeskAI.Application.Features.Sales.Services;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Domain.Enums.Payment;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// Draft đơn hàng của trợ lý AI: prepare_order (tạo/sửa), confirm_order, discard_order_draft và block prompt.
/// Mục tiêu chính: AI không bị kẹt vòng "hỏi xác nhận mãi mà không đặt", và không bao giờ tạo đơn user chưa thấy.
/// </summary>
public class AiOrderDraftToolTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ChatboxId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid DefaultAddressId = Guid.NewGuid();

    // ── prepare_order ──────────────────────────────────────────────────────────

    [Fact]
    public async Task PrepareOrder_NoDraft_CreatesDraftAndFlagsTurn()
    {
        var f = new Fixture();
        var ctx = Context();

        var json = await f.Prepare().ExecuteAsync(ctx, Args(new { productId = ProductId, quantity = 2 }));

        Assert.Equal("created", Read(json, "action"));
        Assert.NotNull(f.Added);
        Assert.Equal(ItemId, f.Added!.ProductItemId);
        Assert.Equal(2, f.Added.Quantity);
        Assert.Equal(DefaultAddressId, f.Added.ShippingAddressId);
        Assert.Equal(PaymentMethod.PayOS, f.Added.PaymentMethod);
        Assert.True(f.Added.ExpiresAt > DateTime.UtcNow.AddMinutes(59));
        Assert.True(ctx.OrderDraftChangedThisTurn);
        f.Drafts.Verify(d => d.DeletePendingAsync(UserId, ChatboxId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PrepareOrder_ExistingDraft_OnlyQuantityGiven_KeepsOtherChoices()
    {
        var f = new Fixture();
        var customAddressId = Guid.NewGuid();
        f.Addresses.Setup(a => a.GetByIdForUserAsync(customAddressId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Address(customAddressId));
        var existing = Draft(quantity: 1, addressId: customAddressId, paymentMethod: PaymentMethod.COD);
        f.Drafts.Setup(d => d.GetPendingForUpdateAsync(UserId, ChatboxId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var json = await f.Prepare().ExecuteAsync(Context(), Args(new { quantity = 3 }));

        Assert.Equal("updated", Read(json, "action"));
        Assert.Equal(3, existing.Quantity);
        Assert.Equal(ItemId, existing.ProductItemId);
        Assert.Equal(customAddressId, existing.ShippingAddressId);
        Assert.Equal(PaymentMethod.COD, existing.PaymentMethod);
        Assert.Null(f.Added); // sửa tại chỗ, không tạo dòng mới
    }

    [Fact]
    public async Task PrepareOrder_SwitchToMultiVariantProduct_AsksForVariantAndKeepsDraft()
    {
        var f = new Fixture();
        var otherProductId = Guid.NewGuid();
        f.Products.Setup(p => p.GetByIdAsync(otherProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<ProductDetailResponse>.Success(new ProductDetailResponse
            {
                Id = otherProductId, Name = "Tượng Tỳ Hưu", IsActive = true,
                Items = new() { Item(Guid.NewGuid(), 200_000m), Item(Guid.NewGuid(), 300_000m) },
            }));
        var existing = Draft();
        f.Drafts.Setup(d => d.GetPendingForUpdateAsync(UserId, ChatboxId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var json = await f.Prepare().ExecuteAsync(Context(), Args(new { productId = otherProductId }));

        Assert.Contains("variant", json);
        Assert.Equal(ProductId, existing.ProductId); // draft cũ chưa bị đụng
        f.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PrepareOrder_NoDraftAndNoProductId_ReturnsError()
    {
        var json = await new Fixture().Prepare().ExecuteAsync(Context(), Args(new { quantity = 2 }));

        Assert.NotNull(Read(json, "error"));
    }

    [Fact]
    public async Task PrepareOrder_InvalidGuid_DoesNotFallBackToDraft()
    {
        var f = new Fixture();
        f.Drafts.Setup(d => d.GetPendingForUpdateAsync(UserId, ChatboxId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Draft());

        var json = await f.Prepare().ExecuteAsync(Context(), Args(new { productId = "P1" }));

        Assert.NotNull(Read(json, "error"));
        f.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void PrepareOrder_Description_SwitchesToEditModeWhenDraftExists()
    {
        var tool = new Fixture().Prepare();

        Assert.DoesNotContain("EDIT", tool.DescribeFor(Context()));
        Assert.Contains("EDIT", tool.DescribeFor(Context(activeDraft: true)));
    }

    // ── confirm_order ──────────────────────────────────────────────────────────

    [Fact]
    public void ConfirmOrder_HiddenUntilDraftExistsAtTurnStart()
    {
        var tool = new Fixture().Confirm();

        Assert.False(tool.IsAvailable(Context()));
        Assert.True(tool.IsAvailable(Context(activeDraft: true)));
    }

    [Fact]
    public async Task ConfirmOrder_DraftChangedThisTurn_IsRejectedWithoutClaiming()
    {
        var f = new Fixture();
        var ctx = Context(activeDraft: true);
        ctx.OrderDraftChangedThisTurn = true;

        var json = await f.Confirm().ExecuteAsync(ctx, Args(new { }));

        Assert.NotNull(Read(json, "error"));
        f.Drafts.Verify(d => d.TryClaimAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Orders.Verify(o => o.CheckoutAsync(It.IsAny<Guid>(), It.IsAny<CheckoutRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmOrder_ClaimLost_DoesNotCheckout()
    {
        var f = new Fixture().WithPendingDraft(Draft());
        f.Drafts.Setup(d => d.TryClaimAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var json = await f.Confirm().ExecuteAsync(Context(activeDraft: true), Args(new { }));

        Assert.NotNull(Read(json, "error"));
        f.Orders.Verify(o => o.CheckoutAsync(It.IsAny<Guid>(), It.IsAny<CheckoutRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmOrder_PriceChanged_KeepsDraftWithNewPriceAndBlocksSameTurnRetry()
    {
        var draft = Draft(quantity: 2); // snapshot 150k x2 = 300k
        var f = new Fixture().WithPendingDraft(draft).WithPreview(subtotal: 360_000m);
        var ctx = Context(activeDraft: true);

        var json = await f.Confirm().ExecuteAsync(ctx, Args(new { }));

        Assert.NotNull(Read(json, "error"));
        f.Drafts.Verify(d => d.ReleaseClaimWithNewPriceAsync(draft.Id, 180_000m, It.IsAny<decimal>(), It.IsAny<decimal>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        f.Drafts.Verify(d => d.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Orders.Verify(o => o.CheckoutAsync(It.IsAny<Guid>(), It.IsAny<CheckoutRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.True(ctx.OrderDraftChangedThisTurn);
    }

    [Fact]
    public async Task ConfirmOrder_CheckoutFails_ReleasesClaimAndKeepsDraft()
    {
        var draft = Draft();
        var f = new Fixture().WithPendingDraft(draft).WithPreview(subtotal: 150_000m);
        f.Orders.Setup(o => o.CheckoutAsync(UserId, It.IsAny<CheckoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<OrderDetailResponse>.Failure(422, "Hết hàng"));

        var json = await f.Confirm().ExecuteAsync(Context(activeDraft: true), Args(new { }));

        Assert.Contains("Hết hàng", Read(json, "error"));
        f.Drafts.Verify(d => d.ReleaseClaimAsync(draft.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        f.Drafts.Verify(d => d.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmOrder_Success_UsesDraftPaymentMethodAndDeletesDraft()
    {
        var draft = Draft(paymentMethod: PaymentMethod.COD);
        var f = new Fixture().WithPendingDraft(draft).WithPreview(subtotal: 150_000m);
        CheckoutRequest? sent = null;
        f.Orders.Setup(o => o.CheckoutAsync(UserId, It.IsAny<CheckoutRequest>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, CheckoutRequest, CancellationToken>((_, r, _) => sent = r)
            .ReturnsAsync(ServiceResult<OrderDetailResponse>.Success(new OrderDetailResponse { Id = Guid.NewGuid() }));

        var json = await f.Confirm().ExecuteAsync(Context(activeDraft: true), Args(new { }));

        Assert.Null(Read(json, "error"));
        Assert.Equal(PaymentMethod.COD, sent!.PaymentMethod);
        Assert.Equal(draft.ShippingAddressId, sent.ShippingAddressId);
        f.Drafts.Verify(d => d.DeleteAsync(draft.Id, It.IsAny<CancellationToken>()), Times.Once);
        f.Payments.Verify(p => p.CreatePaymentAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── discard_order_draft ────────────────────────────────────────────────────

    [Fact]
    public async Task DiscardDraft_DeletesPendingDraftOfRoom()
    {
        var f = new Fixture();
        f.Drafts.Setup(d => d.DeletePendingAsync(UserId, ChatboxId, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var tool = new DiscardOrderDraftTool(f.Uow.Object);
        var ctx = Context(activeDraft: true);

        var json = await tool.ExecuteAsync(ctx, Args(new { }));

        Assert.Null(Read(json, "error"));
        Assert.True(ctx.OrderDraftChangedThisTurn);
        Assert.False(tool.IsAvailable(Context()));
    }

    // ── block prompt ───────────────────────────────────────────────────────────

    [Fact]
    public void DraftPrompt_ContainsChoicesProductLinkAndEscapesPipes()
    {
        var now = DateTime.UtcNow;
        var draftRef = new AiOrderDraftRef(ProductId, "Cây Kim Tiền", "Chậu | sứ", 2, 150_000m, 30_000m, 330_000m,
            "Nguyễn A (0900000000) - 1 Lê Lợi", "COD", now.AddMinutes(42));

        var prompt = AiOrderDraftPrompt.Build(draftRef, now);

        Assert.Contains("CURRENT ORDER DRAFT", prompt);
        Assert.Contains($"[Cây Kim Tiền](/products/{ProductId})", prompt);
        Assert.Contains("Chậu / sứ", prompt);
        Assert.Contains("expires in 42 min", prompt);
        Assert.Contains("COD", prompt);
        Assert.Contains("confirm_order", prompt);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static AiToolContext Context(bool activeDraft = false) => new(UserId, "Customer", null, ChatboxId)
    {
        ActiveOrderDraft = activeDraft
            ? new AiOrderDraftRef(ProductId, "Cây Kim Tiền", null, 1, 150_000m, 30_000m, 180_000m, "addr", "PayOS", DateTime.UtcNow.AddMinutes(30))
            : null,
    };

    private static JsonElement Args(object value) => JsonSerializer.SerializeToElement(value);

    private static string? Read(string json, string property)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty(property, out var v) ? v.ToString() : null;
    }

    private static ProductItemResponse Item(Guid id, decimal price) => new() { Id = id, Name = "Chậu sứ", Price = price, Stock = 5 };

    private static AiOrderDraft Draft(int quantity = 1, Guid? addressId = null, PaymentMethod paymentMethod = PaymentMethod.PayOS) => new()
    {
        UserId = UserId,
        ChatboxId = ChatboxId,
        ProductId = ProductId,
        ProductItemId = ItemId,
        Quantity = quantity,
        ShippingAddressId = addressId ?? DefaultAddressId,
        PaymentMethod = paymentMethod,
        UnitPriceSnapshot = 150_000m,
        ProductName = "Cây Kim Tiền",
        AddressText = "addr",
        ExpiresAt = DateTime.UtcNow.AddMinutes(30),
    };

    private static UserAddress Address(Guid id) => new()
    {
        Id = id,
        UserId = UserId,
        RecipientName = "Nguyễn A",
        RecipientPhone = "0900000000",
        StreetAddress = "1 Lê Lợi",
        Ward = new Ward { Name = "Bến Nghé", District = new District { Name = "Quận 1", Province = new Province { Name = "TP.HCM" } } },
    };

    private sealed class Fixture
    {
        public Mock<IUnitOfWork> Uow { get; } = new();
        public Mock<IAiOrderDraftRepository> Drafts { get; } = new();
        public Mock<IUserAddressRepository> Addresses { get; } = new();
        public Mock<IProductService> Products { get; } = new();
        public Mock<IOrderService> Orders { get; } = new();
        public Mock<IPaymentService> Payments { get; } = new();
        public AiOrderDraft? Added { get; private set; }

        public Fixture()
        {
            Uow.SetupGet(u => u.AiOrderDrafts).Returns(Drafts.Object);
            Uow.SetupGet(u => u.UserAddresses).Returns(Addresses.Object);
            Uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            Drafts.Setup(d => d.AddAsync(It.IsAny<AiOrderDraft>(), It.IsAny<CancellationToken>()))
                .Callback<AiOrderDraft, CancellationToken>((d, _) => Added = d)
                .Returns(Task.CompletedTask);
            Drafts.Setup(d => d.TryClaimAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            Addresses.Setup(a => a.GetDefaultForUserAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Address(DefaultAddressId));
            Addresses.Setup(a => a.GetWithWardChainAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => Address(id));

            Products.Setup(p => p.GetByIdAsync(ProductId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<ProductDetailResponse>.Success(new ProductDetailResponse
                {
                    Id = ProductId, Name = "Cây Kim Tiền", IsActive = true, Items = new() { Item(ItemId, 150_000m) },
                }));

            WithPreview(subtotal: 150_000m);
        }

        public Fixture WithPendingDraft(AiOrderDraft draft)
        {
            Drafts.Setup(d => d.GetPendingAsync(UserId, ChatboxId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(draft);
            return this;
        }

        public Fixture WithPreview(decimal subtotal)
        {
            Orders.Setup(o => o.PreviewShippingFeeAsync(UserId, It.IsAny<CheckoutRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<ShippingFeePreviewResponse>.Success(new ShippingFeePreviewResponse
                {
                    Subtotal = subtotal, TotalShippingFee = 30_000m, TotalAmount = subtotal + 30_000m,
                }));
            return this;
        }

        public PrepareOrderTool Prepare() => new(Products.Object, Orders.Object, Uow.Object,
            Options.Create(new AiOrderDraftOptions { DraftTtlMinutes = 60 }));

        public ConfirmOrderTool Confirm() => new(Orders.Object, Payments.Object, Uow.Object,
            NullLogger<ConfirmOrderTool>.Instance);
    }
}
