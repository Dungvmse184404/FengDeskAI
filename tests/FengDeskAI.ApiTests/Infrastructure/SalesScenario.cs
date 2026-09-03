using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Domain.Entities.Vendor;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FengDeskAI.ApiTests.Infrastructure;

/// <summary>Một cửa hàng đã đủ điều kiện bán hàng, kèm biến thể sản phẩm để đặt.</summary>
public sealed record SeededStore(Guid StoreId, string StoreName, Guid ProductId, Guid ProductItemId, decimal Price, int Stock);

/// <summary>Dữ liệu nền cho các ca test bán hàng.</summary>
public sealed record SalesScenarioData(Guid ShippingAddressId, IReadOnlyList<SeededStore> Stores)
{
    public SeededStore StoreA => Stores[0];
    public SeededStore StoreB => Stores[1];
}

/// <summary>
/// Dựng dữ liệu tối thiểu để đặt được hàng: phường/xã có mã GHN, cửa hàng đủ điều kiện giao,
/// sản phẩm còn tồn, và địa chỉ giao của khách.
///
/// Vì sao ghi thẳng qua DbContext thay vì gọi API: luồng tạo cửa hàng đi qua duyệt/phân quyền và
/// đồng bộ mã shop nhà vận chuyển — dựng qua API sẽ biến mọi ca test bán hàng thành ca test tạo
/// cửa hàng, và hỏng dây chuyền khi luồng đó đổi. Đây là **dữ liệu nền**, không phải thứ đang test.
///
/// Bẫy đã gặp, đừng dẫm lại: <c>GeographySeeder</c> KHÔNG điền <c>GhnWardCode</c> /
/// <c>GhnDistrictId</c> (mã GHN chỉ có khi chạy sync-geo với mạng ngoài). Thiếu hai mã đó thì
/// <c>StoreShippingReadiness</c> chặn ngay ở bước đặt hàng với lỗi "chưa có mã vùng của nhà vận
/// chuyển" — nên fixture phải tự điền.
/// </summary>
public static class SalesScenario
{
    /// <summary>Số di động hợp lệ với nhà vận chuyển — hotline 1900 hay số cố định đều bị từ chối.</summary>
    private const string CarrierValidPhone = "0901234567";

    public static async Task<SalesScenarioData> SeedAsync(ApiTestFixture fixture, Guid customerId, int storeCount = 2)
    {
        SalesScenarioData? result = null;

        await fixture.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();

            var ward = await EnsureWardWithCarrierCodesAsync(db);
            var ownerId = await OwnerIdAsync(db);

            var stores = new List<SeededStore>();
            for (var i = 0; i < storeCount; i++)
                stores.Add(await CreateStoreWithProductAsync(db, ward.Id, ownerId, i));

            var addressId = await EnsureShippingAddressAsync(db, customerId, ward.Id);

            await db.SaveChangesAsync();
            result = new SalesScenarioData(addressId, stores);
        });

        return result!;
    }

    /// <summary>Lấy một phường/xã bất kỳ và điền mã GHN cho nó (seeder không điền).</summary>
    private static async Task<Ward> EnsureWardWithCarrierCodesAsync(AppDbContext db)
    {
        var ward = await db.Set<Ward>().Include(w => w.District).OrderBy(w => w.Name).FirstOrDefaultAsync()
                   ?? throw new InvalidOperationException(
                       "Chưa có dữ liệu phường/xã. GeographySeeder phải chạy trước — kiểm tra RunSeedersAsync trong ApiTestFixture.");

        ward.GhnWardCode ??= "20308";
        ward.District.GhnDistrictId ??= 1442;
        return ward;
    }

    private static async Task<Guid> OwnerIdAsync(AppDbContext db)
    {
        var email = ApiTestFixture.EmailFor(TestRole.GardenOwner);
        var owner = await db.Users.FirstOrDefaultAsync(u => u.Email == email)
                    ?? throw new InvalidOperationException($"Chưa có user mẫu {email}.");
        return owner.Id;
    }

    private static async Task<SeededStore> CreateStoreWithProductAsync(AppDbContext db, Guid wardId, Guid ownerId, int index)
    {
        var suffix = $"{index + 1}-{Guid.NewGuid():N}"[..12];

        var store = new GardenStore
        {
            Name = $"Vườn kiểm thử {suffix}",
            Description = "Cửa hàng dựng cho ca test bán hàng.",
            Hotline = CarrierValidPhone,
            OpeningHours = "08:00 - 21:00",
            IsActive = true,
        };
        store.Owners.Add(new GardenStoreOwner { OwnerUserId = ownerId, IsPrimary = true, AssignedAt = DateTime.UtcNow });
        await db.Set<GardenStore>().AddAsync(store);

        await db.Set<StoreAddress>().AddAsync(new StoreAddress
        {
            StoreId = store.Id,
            WardId = wardId,
            StreetAddress = $"{index + 1} Đường Kiểm Thử",
            SenderName = store.Name,
            SenderPhone = CarrierValidPhone,
            IsActive = true,
        });

        var product = new Product
        {
            GardenStoreId = store.Id,
            Name = $"Cây kiểm thử {suffix}",
            Description = "Sản phẩm dựng cho ca test bán hàng.",
            IsActive = true,
        };
        var item = new ProductItem { Name = "Chậu tiêu chuẩn", Price = 150_000m, Stock = 50, Sku = $"TEST-{suffix}" };
        product.Items.Add(item);
        await db.Set<Product>().AddAsync(product);

        return new SeededStore(store.Id, store.Name, product.Id, item.Id, item.Price, item.Stock);
    }

    private static async Task<Guid> EnsureShippingAddressAsync(AppDbContext db, Guid customerId, Guid wardId)
    {
        var existing = await db.Set<UserAddress>()
            .FirstOrDefaultAsync(a => a.UserId == customerId && a.IsDefault);
        if (existing is not null)
        {
            existing.WardId = wardId;
            existing.RecipientPhone = CarrierValidPhone;
            return existing.Id;
        }

        var address = new UserAddress
        {
            UserId = customerId,
            WardId = wardId,
            StreetAddress = "88 Đường Người Nhận",
            RecipientName = "Khách kiểm thử",
            RecipientPhone = CarrierValidPhone,
            IsDefault = true,
            Label = "Nhà",
        };
        await db.Set<UserAddress>().AddAsync(address);
        return address.Id;
    }
}
