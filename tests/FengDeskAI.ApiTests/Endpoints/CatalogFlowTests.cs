using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.ApiTests.Infrastructure;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Đợt 5 — Catalog: vòng đời sản phẩm (tạo → biến thể → ảnh → danh mục → phong thủy → xóa),
/// danh mục và tag, kèm ranh giới phân quyền theo cửa hàng.
///
/// Ghi chú về phân quyền, để khỏi ngạc nhiên khi đọc assert:
/// mọi endpoint ghi của <c>/api/products/{id}</c> đi qua filter <c>ResourceAuthorize</c>, chạy TRƯỚC
/// action — bị chặn thì trả 403 với THÂN RỖNG, không phải phong bì. Id không tồn tại cũng ra 403
/// (filter không phân biệt "không có" với "không được phép"), nên đừng kỳ vọng 404 ở đó.
/// Riêng <c>POST /api/products</c> không có id trên route nên chỉ có tầng service kiểm tra →
/// trả 403 kèm phong bì bình thường.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class CatalogFlowTests
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public CatalogFlowTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ===================== Sản phẩm: vòng đời =====================

    [Fact(DisplayName = "CAT-01 [Normal] A store owner creates a product with one variant")]
    public async Task CreateProduct_AsStoreOwner_Succeeds()
    {
        var store = await OwnedStoreAsync();

        var response = await Owner().PostAsJsonAsync("/api/products", new
        {
            gardenStoreId = store.StoreId,
            name = "Kim tiền để bàn",
            description = "Cây phong thủy hợp mệnh Mộc.",
            items = new[] { new { name = "Chậu nhỏ", price = 250_000m, stock = 10 } },
        });

        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"{(int)response.StatusCode} {body}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal("Kim tiền để bàn", data.GetProperty("name").GetString());
        Assert.True(data.GetProperty("isActive").GetBoolean());
        Assert.Single(data.GetProperty("items").EnumerateArray());
    }

    [Fact(DisplayName = "CAT-02 [Abnormal] Creating a product with a blank name is rejected")]
    public async Task CreateProduct_BlankName_IsRejected()
    {
        var store = await OwnedStoreAsync();

        var response = await Owner().PostAsJsonAsync("/api/products", new
        {
            gardenStoreId = store.StoreId,
            name = "   ",
            items = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("tên sản phẩm", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "CAT-03 [Abnormal] A customer cannot create a product for someone else's store")]
    public async Task CreateProduct_AsCustomer_IsForbidden()
    {
        var store = await OwnedStoreAsync();

        var response = await _fixture.ClientFor(TestRole.Customer).PostAsJsonAsync("/api/products", new
        {
            gardenStoreId = store.StoreId,
            name = "Sản phẩm lậu",
            items = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "CAT-04 [Abnormal] Creating a product with an unknown category is rejected")]
    public async Task CreateProduct_UnknownCategory_IsRejected()
    {
        var store = await OwnedStoreAsync();

        var response = await Owner().PostAsJsonAsync("/api/products", new
        {
            gardenStoreId = store.StoreId,
            name = "Cây danh mục lạ",
            items = Array.Empty<object>(),
            categoryIds = new[] { Guid.NewGuid() },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("danh mục", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "CAT-05 [Normal] A newly created product is readable anonymously")]
    public async Task GetProduct_Anonymously_ReturnsDetail()
    {
        var productId = await CreateProductAsync();

        var response = await _fixture.ClientFor(TestRole.Anonymous).GetAsync($"/api/products/{productId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal(productId, data.GetProperty("id").GetGuid());
    }

    [Fact(DisplayName = "CAT-06 [Abnormal] Reading an unknown product returns 404")]
    public async Task GetProduct_UnknownId_ReturnsNotFound()
    {
        var response = await _fixture.ClientFor(TestRole.Anonymous).GetAsync($"/api/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "CAT-07 [Normal] The owner updates a product and the change is persisted")]
    public async Task UpdateProduct_AsOwner_PersistsChange()
    {
        var productId = await CreateProductAsync();

        var update = await Owner().PutAsJsonAsync($"/api/products/{productId}", new
        {
            name = "Tên đã đổi",
            description = "Mô tả mới.",
            isActive = false,
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var reread = await _fixture.ClientFor(TestRole.Anonymous).GetAsync($"/api/products/{productId}");
        var data = await ApiEnvelope.DataAsync(reread);
        Assert.Equal("Tên đã đổi", data.GetProperty("name").GetString());
        Assert.False(data.GetProperty("isActive").GetBoolean());
    }

    [Fact(DisplayName = "CAT-08 [Abnormal] A customer cannot update a product they do not own")]
    public async Task UpdateProduct_AsCustomer_IsForbidden()
    {
        var productId = await CreateProductAsync();

        var response = await _fixture.ClientFor(TestRole.Customer).PutAsJsonAsync($"/api/products/{productId}", new
        {
            name = "Chiếm quyền",
            isActive = true,
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "CAT-09 [Normal] Deleting a product removes it from the public detail endpoint")]
    public async Task DeleteProduct_AsOwner_HidesProduct()
    {
        var productId = await CreateProductAsync();

        var delete = await Owner().DeleteAsync($"/api/products/{productId}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var reread = await _fixture.ClientFor(TestRole.Anonymous).GetAsync($"/api/products/{productId}");
        Assert.Equal(HttpStatusCode.NotFound, reread.StatusCode);
    }

    [Fact(DisplayName = "CAT-09b [Boundary] A product still inside an open order (return window) cannot be deleted, soft or hard")]
    public async Task DeleteProduct_OpenOrder_IsRejected()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture); // vừa giao ⇒ còn trong 7 ngày đổi trả
        var (productId, itemId) = await ProductOfOrderItemAsync(order.OrderItemId);

        var soft = await Owner().DeleteAsync($"/api/products/{productId}");
        var softItem = await Owner().DeleteAsync($"/api/products/{productId}/items/{itemId}");
        var hard = await _fixture.ClientFor(TestRole.Manager).DeleteAsync($"/api/products/{productId}/permanent");

        Assert.Equal(HttpStatusCode.Conflict, soft.StatusCode);
        Assert.Contains("Ngừng bán", await ApiEnvelope.MessageAsync(soft));
        Assert.Equal(HttpStatusCode.Conflict, softItem.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, hard.StatusCode);
    }

    [Fact(DisplayName = "CAT-09d [Normal] After the return window a closed order survives soft then hard delete intact; the review is kept")]
    public async Task DeleteProduct_ClosedOrder_SoftThenHard_OrderAndReviewIntact()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);
        var (productId, itemId) = await ProductOfOrderItemAsync(order.OrderItemId);
        var customer = _fixture.ClientFor(TestRole.Customer);
        var review = await customer.PostAsJsonAsync("/api/review", new { productId, rating = 5, content = "Cây đẹp" });
        Assert.True(review.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(review, "đánh giá"));
        var before = await OrderLineAsync(customer, order.OrderId);
        await CloseReturnWindowAsync(order.DeliveryId);

        // Người bán xoá = xoá mềm; một khách khác đang để biến thể trong giỏ.
        var shopper = ScenarioUsers.ClientFor(_fixture, await ScenarioUsers.CreateAsync(_fixture));
        Assert.True((await shopper.PostAsJsonAsync("/api/cart/items", new { productItemId = itemId, quantity = 1 })).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Owner().DeleteAsync($"/api/products/{productId}")).StatusCode);

        var afterSoft = await OrderLineAsync(customer, order.OrderId);
        AssertSameLine(before, afterSoft);
        Assert.False(afterSoft.GetProperty("productAvailable").GetBoolean());
        Assert.Empty((await ApiEnvelope.DataAsync(await shopper.GetAsync("/api/cart"))).GetProperty("items").EnumerateArray());

        // Manager xoá vĩnh viễn — kể cả sản phẩm đã bị xoá mềm.
        var hard = await _fixture.ClientFor(TestRole.Manager).DeleteAsync($"/api/products/{productId}/permanent");
        Assert.Equal(HttpStatusCode.OK, hard.StatusCode);

        var afterHard = await OrderLineAsync(customer, order.OrderId);
        AssertSameLine(before, afterHard);
        Assert.Equal(JsonValueKind.Null, afterHard.GetProperty("productItemId").ValueKind);
        bool productRowGone = false;
        string? reviewName = null;
        await _fixture.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            productRowGone = !await db.Set<Domain.Entities.Catalog.Product>().IgnoreQueryFilters().AnyAsync(p => p.Id == productId);
            reviewName = await db.Reviews.Where(r => r.UserId == _fixture.UserId(TestRole.Customer) && r.ProductId == null
                                                    && r.ProductName != null && r.Content == "Cây đẹp")
                .Select(r => r.ProductName).FirstOrDefaultAsync();
        });
        Assert.True(productRowGone, "xoá cứng phải xoá hẳn dòng sản phẩm");
        Assert.NotNull(reviewName);
    }

    [Fact(DisplayName = "CAT-09e [Security] Only managers can permanently delete a product")]
    public async Task HardDelete_AsGardenOwner_IsForbidden()
    {
        var productId = await CreateProductAsync();

        var response = await Owner().DeleteAsync($"/api/products/{productId}/permanent");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "CAT-09c [Normal] Deleting a never-ordered product also drops its variants from shoppers' carts")]
    public async Task DeleteProduct_NoOrders_RemovesFromCarts()
    {
        var productId = await CreateProductAsync();
        var itemId = await FirstItemIdAsync(productId);
        var shopper = ScenarioUsers.ClientFor(_fixture, await ScenarioUsers.CreateAsync(_fixture));
        var add = await shopper.PostAsJsonAsync("/api/cart/items", new { productItemId = itemId, quantity = 1 });
        Assert.True(add.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(add, "thêm vào giỏ"));

        var delete = await Owner().DeleteAsync($"/api/products/{productId}");

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        bool inCart = true, itemAlive = true;
        await _fixture.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            inCart = await db.CartItems.AnyAsync(c => c.ProductItemId == itemId);
            itemAlive = await db.Set<Domain.Entities.Catalog.ProductItem>().AnyAsync(i => i.Id == itemId);
        });
        Assert.False(inCart, "biến thể của sản phẩm đã xoá phải rời khỏi giỏ");
        Assert.False(itemAlive, "biến thể phải bị xoá mềm theo sản phẩm");
    }

    private async Task<(Guid ProductId, Guid ItemId)> ProductOfOrderItemAsync(Guid orderItemId)
    {
        (Guid, Guid) result = default;
        await _fixture.WithScopeAsync(async sp => result = await sp.GetRequiredService<AppDbContext>().OrderItems
            .Where(i => i.Id == orderItemId)
            .Select(i => new ValueTuple<Guid, Guid>(i.ProductId!.Value, i.ProductItemId!.Value))
            .SingleAsync());
        return result;
    }

    /// <summary>Đẩy mốc giao về quá khoảng đổi trả ⇒ đơn coi như đã đóng.</summary>
    private async Task CloseReturnWindowAsync(Guid deliveryId)
        => await _fixture.WithScopeAsync(async sp => await sp.GetRequiredService<AppDbContext>().Deliveries
            .Where(d => d.Id == deliveryId)
            .ExecuteUpdateAsync(u => u.SetProperty(d => d.DeliveredAt, DateTime.UtcNow.AddDays(-30))));

    private static async Task<JsonElement> OrderLineAsync(HttpClient customer, Guid orderId)
    {
        var detail = await ApiEnvelope.DataAsync(await customer.GetAsync($"/api/orders/{orderId}"));
        Assert.Equal(1, detail.GetProperty("items").GetArrayLength());
        return detail.GetProperty("items")[0].Clone();
    }

    /// <summary>Món trong đơn phải y nguyên: tên, biến thể, ảnh, giá, số lượng.</summary>
    private static void AssertSameLine(JsonElement expected, JsonElement actual)
    {
        foreach (var field in new[] { "productName", "variantName", "imageUrl", "unitPrice", "quantity", "productId" })
            Assert.Equal(expected.GetProperty(field).ToString(), actual.GetProperty(field).ToString());
    }

    // ===================== Biến thể =====================

    [Fact(DisplayName = "CAT-10 [Normal] The owner adds a variant to an existing product")]
    public async Task AddItem_AsOwner_Succeeds()
    {
        var productId = await CreateProductAsync();

        var response = await Owner().PostAsJsonAsync($"/api/products/{productId}/items", new
        {
            name = "Chậu lớn",
            price = 500_000m,
            stock = 3,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal(500_000m, data.GetProperty("price").GetDecimal());
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("sku").GetString()),
            "SKU để trống thì hệ thống phải tự sinh, không được trả về rỗng.");
    }

    [Fact(DisplayName = "CAT-11 [Abnormal] A variant with a negative price is rejected")]
    public async Task AddItem_NegativePrice_IsRejected()
    {
        var productId = await CreateProductAsync();

        var response = await Owner().PostAsJsonAsync($"/api/products/{productId}/items", new
        {
            name = "Chậu giá âm",
            price = -1m,
            stock = 1,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("giá", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "CAT-12 [Abnormal] Reusing an existing SKU on another variant is rejected")]
    public async Task AddItem_DuplicateSku_IsRejected()
    {
        var productId = await CreateProductAsync();
        var sku = $"DUP-{Guid.NewGuid():N}"[..12];

        var first = await Owner().PostAsJsonAsync($"/api/products/{productId}/items",
            new { name = "Bản A", price = 100_000m, stock = 1, sku });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await Owner().PostAsJsonAsync($"/api/products/{productId}/items",
            new { name = "Bản B", price = 100_000m, stock = 1, sku });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains("SKU", await ApiEnvelope.MessageAsync(second), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "CAT-12b [Normal] SKU suggestion is a platform code that can be saved as-is")]
    public async Task SuggestSku_ReturnsUnusedPlatformCode_Accepted()
    {
        var productId = await CreateProductAsync();

        var suggestion = await ApiEnvelope.DataAsync(await Owner().GetAsync("/api/products/sku-suggestion"));
        var sku = suggestion.GetString()!;
        Assert.Matches("^FD-[0-9A-HJKMNP-TV-Z]{8}$", sku);

        var saved = await Owner().PostAsJsonAsync($"/api/products/{productId}/items",
            new { name = "Bản gợi ý", price = 100_000m, stock = 1, sku });
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        Assert.Equal(sku, (await ApiEnvelope.DataAsync(saved)).GetProperty("sku").GetString());
    }

    [Fact(DisplayName = "CAT-13 [Normal] Updating a variant changes its price and stock")]
    public async Task UpdateItem_AsOwner_PersistsChange()
    {
        var productId = await CreateProductAsync();
        var itemId = await FirstItemIdAsync(productId);

        var response = await Owner().PutAsJsonAsync($"/api/products/{productId}/items/{itemId}", new
        {
            name = "Chậu đã sửa",
            price = 320_000m,
            stock = 7,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal(320_000m, data.GetProperty("price").GetDecimal());
        Assert.Equal(7, data.GetProperty("stock").GetInt32());
    }

    [Fact(DisplayName = "CAT-14 [Abnormal] Updating an unknown variant returns 404")]
    public async Task UpdateItem_UnknownId_ReturnsNotFound()
    {
        var productId = await CreateProductAsync();

        var response = await Owner().PutAsJsonAsync($"/api/products/{productId}/items/{Guid.NewGuid()}", new
        {
            name = "Không tồn tại",
            price = 1_000m,
            stock = 1,
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ===================== Ảnh =====================

    [Fact(DisplayName = "CAT-15 [Normal] The owner attaches an image by URL")]
    public async Task AddImageByLink_AsOwner_Succeeds()
    {
        var productId = await CreateProductAsync();

        var response = await Owner().PostAsJsonAsync($"/api/products/{productId}/images/link", new
        {
            url = "https://fake-storage.test/bucket/product/anh-1.jpg",
            sortOrder = 0,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal("https://fake-storage.test/bucket/product/anh-1.jpg", data.GetProperty("url").GetString());
    }

    [Fact(DisplayName = "CAT-16 [Abnormal] Attaching an image with a blank URL is rejected")]
    public async Task AddImageByLink_BlankUrl_IsRejected()
    {
        var productId = await CreateProductAsync();

        var response = await Owner().PostAsJsonAsync($"/api/products/{productId}/images/link",
            new { url = "  ", sortOrder = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "CAT-17 [Normal] The owner deletes an image that has no 3D model")]
    public async Task DeleteImage_WithoutModel3D_Succeeds()
    {
        var productId = await CreateProductAsync();
        var imageId = await AddImageAsync(productId);

        var response = await Owner().DeleteAsync($"/api/products/{productId}/images/{imageId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ===================== Danh mục & phong thủy của sản phẩm =====================

    [Fact(DisplayName = "CAT-18 [Normal] Assigning categories to a product is reflected in its detail")]
    public async Task SetCategories_AsOwner_PersistsChange()
    {
        var productId = await CreateProductAsync();
        var categoryId = await CreateCategoryAsync();

        var response = await Owner().PutAsJsonAsync($"/api/products/{productId}/categories",
            new { categoryIds = new[] { categoryId } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Contains(data.GetProperty("categories").EnumerateArray(),
            c => c.GetProperty("id").GetGuid() == categoryId);
    }

    [Fact(DisplayName = "CAT-19 [Normal] Setting the feng-shui attributes drops a duplicated primary element")]
    public async Task SetFengShui_DuplicatedPrimary_IsDeduplicated()
    {
        var productId = await CreateProductAsync();

        var response = await Owner().PutAsJsonAsync($"/api/products/{productId}/feng-shui", new
        {
            primaryElement = "Moc",
            secondaryElements = new[] { "Moc", "Thuy" },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal("Moc", data.GetProperty("primaryElement").GetString());

        var secondary = data.GetProperty("secondaryElements").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        Assert.DoesNotContain("Moc", secondary);
        Assert.Contains("Thuy", secondary);
    }

    [Fact(DisplayName = "CAT-20 [Abnormal] Setting an unknown vibe code is rejected")]
    public async Task SetFengShui_UnknownVibe_IsRejected()
    {
        var productId = await CreateProductAsync();

        var response = await Owner().PutAsJsonAsync($"/api/products/{productId}/feng-shui", new
        {
            primaryElement = "Kim",
            vibes = new[] { "khong-ton-tai-vibe" },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // So với HẰNG SỐ chứ không so với một từ trong câu: câu chữ hiển thị cho người dùng được
        // phép sửa (và đã sửa - "vibe" là từ trong code, không đưa lên giao diện), còn việc endpoint
        // trả đúng thông báo nào mới là thứ ca này khoá.
        Assert.Equal(
            ApiStatusMessages.Product.VibesNotExist,
            await ApiEnvelope.MessageAsync(response));
    }

    // ===================== Vector ngũ hành =====================

    [Fact(DisplayName = "CAT-21 [Normal] Overriding the element vector marks the product as overridden")]
    public async Task SetVectorOverride_ValidVector_MarksOverridden()
    {
        var productId = await CreateProductAsync();

        var response = await Owner().PutAsJsonAsync($"/api/products/{productId}/vector-override", new
        {
            tho = 0.2m, kim = 0.2m, thuy = 0.2m, moc = 0.2m, hoa = 0.2m,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.True(data.GetProperty("isVectorOverridden").GetBoolean());
    }

    [Fact(DisplayName = "CAT-22 [Boundary] An element vector that does not sum to 1.0 is rejected")]
    public async Task SetVectorOverride_SumNotOne_IsRejected()
    {
        var productId = await CreateProductAsync();

        var response = await Owner().PutAsJsonAsync($"/api/products/{productId}/vector-override", new
        {
            tho = 0.5m, kim = 0.5m, thuy = 0.5m, moc = 0.5m, hoa = 0.5m,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "CAT-23 [Normal] Clearing the override returns the product to the computed vector")]
    public async Task ClearVectorOverride_AfterOverride_ClearsFlag()
    {
        var productId = await CreateProductAsync();
        await Owner().PutAsJsonAsync($"/api/products/{productId}/vector-override",
            new { tho = 0.2m, kim = 0.2m, thuy = 0.2m, moc = 0.2m, hoa = 0.2m });

        var response = await Owner().DeleteAsync($"/api/products/{productId}/vector-override");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.False(data.GetProperty("isVectorOverridden").GetBoolean());
    }

    // ===================== Danh mục =====================

    [Fact(DisplayName = "CAT-24 [Normal] A manager creates a category and it appears in the public list")]
    public async Task CreateCategory_AsManager_AppearsInList()
    {
        var name = $"Danh mục {Guid.NewGuid():N}"[..20];

        var create = await _fixture.ClientFor(TestRole.Manager)
            .PostAsJsonAsync("/api/categories", new { name, description = "Sinh trong test." });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var list = await _fixture.ClientFor(TestRole.Anonymous).GetAsync("/api/categories");
        var data = await ApiEnvelope.DataAsync(list);
        Assert.Contains(data.EnumerateArray(), c => c.GetProperty("name").GetString() == name);
    }

    [Fact(DisplayName = "CAT-25 [Abnormal] A customer cannot create a category")]
    public async Task CreateCategory_AsCustomer_IsForbidden()
    {
        var response = await _fixture.ClientFor(TestRole.Customer)
            .PostAsJsonAsync("/api/categories", new { name = "Không được phép" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "CAT-26 [Abnormal] Platform staff cannot create a category — the policy is manager or above")]
    public async Task CreateCategory_AsStaff_IsForbidden()
    {
        var response = await _fixture.ClientFor(TestRole.Staff)
            .PostAsJsonAsync("/api/categories", new { name = "Staff thử" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "CAT-27 [Abnormal] A category cannot be its own parent")]
    public async Task UpdateCategory_SelfAsParent_IsRejected()
    {
        var categoryId = await CreateCategoryAsync();

        var response = await _fixture.ClientFor(TestRole.Manager)
            .PutAsJsonAsync($"/api/categories/{categoryId}", new
            {
                name = "Tự làm cha",
                parentId = categoryId,
                isActive = true,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "CAT-28 [Abnormal] A category with an unknown parent is rejected")]
    public async Task CreateCategory_UnknownParent_IsRejected()
    {
        var response = await _fixture.ClientFor(TestRole.Manager)
            .PostAsJsonAsync("/api/categories", new { name = "Con mồ côi", parentId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ===================== Tag =====================

    [Fact(DisplayName = "CAT-29 [Normal] A garden owner creates a tag")]
    public async Task CreateTag_AsGardenOwner_Succeeds()
    {
        var response = await Owner().PostAsJsonAsync("/api/tags",
            new { name = $"tag-{Guid.NewGuid():N}"[..14] });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(DisplayName = "CAT-30 [Abnormal] Creating a tag whose name already exists returns 409")]
    public async Task CreateTag_DuplicateName_ReturnsConflict()
    {
        var name = $"tag-{Guid.NewGuid():N}"[..14];

        var first = await Owner().PostAsJsonAsync("/api/tags", new { name });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await Owner().PostAsJsonAsync("/api/tags", new { name });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact(DisplayName = "CAT-31 [Abnormal] A plain customer cannot create a tag")]
    public async Task CreateTag_AsCustomer_IsForbidden()
    {
        var response = await _fixture.ClientFor(TestRole.Customer)
            .PostAsJsonAsync("/api/tags", new { name = "khach-thu" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ===================== Helper =====================

    private HttpClient Owner() => _fixture.ClientFor(TestRole.GardenOwner);

    /// <summary>Cửa hàng do user mẫu GardenOwner sở hữu — dựng qua <see cref="SalesScenario"/> để
    /// khỏi phải đi luồng tạo cửa hàng (luồng đó có test riêng ở <c>StoreFlowTests</c>).</summary>
    private async Task<SeededStore> OwnedStoreAsync()
    {
        var data = await SalesScenario.SeedAsync(_fixture, _fixture.UserId(TestRole.Customer), storeCount: 1);
        return data.Stores[0];
    }

    private async Task<Guid> CreateProductAsync()
    {
        var store = await OwnedStoreAsync();
        var response = await Owner().PostAsJsonAsync("/api/products", new
        {
            gardenStoreId = store.StoreId,
            name = $"Sản phẩm {Guid.NewGuid():N}"[..24],
            items = new[] { new { name = "Chậu tiêu chuẩn", price = 199_000m, stock = 5 } },
        });

        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "tạo sản phẩm"));
        return (await ApiEnvelope.DataAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> FirstItemIdAsync(Guid productId)
    {
        var response = await _fixture.ClientFor(TestRole.Anonymous).GetAsync($"/api/products/{productId}");
        var data = await ApiEnvelope.DataAsync(response);
        return data.GetProperty("items")[0].GetProperty("id").GetGuid();
    }

    private async Task<Guid> AddImageAsync(Guid productId)
    {
        var response = await Owner().PostAsJsonAsync($"/api/products/{productId}/images/link",
            new { url = $"https://fake-storage.test/bucket/product/{Guid.NewGuid():N}.jpg", sortOrder = 0 });

        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "gắn ảnh"));
        return (await ApiEnvelope.DataAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateCategoryAsync()
    {
        var response = await _fixture.ClientFor(TestRole.Manager).PostAsJsonAsync("/api/categories",
            new { name = $"DM {Guid.NewGuid():N}"[..16] });

        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "tạo danh mục"));
        return (await ApiEnvelope.DataAsync(response)).GetProperty("id").GetGuid();
    }
}
