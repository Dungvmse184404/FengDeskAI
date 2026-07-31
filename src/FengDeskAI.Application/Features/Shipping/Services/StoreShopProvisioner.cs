using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Validation;
using FengDeskAI.Application.Features.Shipping.DTOs;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Vendor;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Application.Features.Shipping.Services;

/// <summary>
/// Tự cấp mã shop nhà vận chuyển (<c>GardenStore.GhnShopId</c>) cho store chưa có.
/// Mỗi store là một điểm lấy hàng riêng bên GHN — ShopId quyết định địa chỉ shipper đến lấy
/// và tài khoản đối soát COD, nên KHÔNG được dùng chung giữa các vendor.
/// Xem docs/adr/fix-ghn-create-shipment.md §B.
/// </summary>
public interface IStoreShopProvisioner
{
    /// <summary>
    /// Đảm bảo store có mã shop. Trả true nếu sau lời gọi store đã có mã (sẵn có hoặc vừa đăng ký).
    /// Best-effort: lỗi nhà vận chuyển chỉ được log, KHÔNG ném ra để không làm hỏng request gọi nó.
    /// <paramref name="store"/> phải nạp kèm <c>Address.Ward.District</c>.
    /// </summary>
    Task<bool> EnsureShopIdAsync(GardenStore store, CancellationToken ct = default);

    /// <summary>
    /// Backfill hàng loạt các store đang thiếu mã shop. Chỉ gửi request đăng ký cho store đã đủ
    /// điều kiện; store không đủ được liệt kê trong <c>Skipped</c> kèm lý do thay vì gọi vô ích.
    /// </summary>
    Task<CarrierShopSyncResultResponse> BackfillAsync(int batchSize, CancellationToken ct = default);
}

public class StoreShopProvisioner : IStoreShopProvisioner
{
    private readonly IUnitOfWork _uow;
    private readonly IShippingProvider _shipping;
    private readonly ILogger<StoreShopProvisioner> _logger;

    public StoreShopProvisioner(IUnitOfWork uow, IShippingProvider shipping, ILogger<StoreShopProvisioner> logger)
    {
        _uow = uow;
        _shipping = shipping;
        _logger = logger;
    }

    public async Task<bool> EnsureShopIdAsync(GardenStore store, CancellationToken ct = default)
    {
        if (store.GhnShopId is > 0) return true;

        var request = BuildRegistration(store);
        if (request is null)
        {
            // Thiếu địa chỉ/mã vùng/SĐT → chưa đăng ký được. Readiness sẽ báo owner bổ sung,
            // sau đó hook ở StoreService gọi lại hàm này.
            _logger.LogInformation("[Shipping] Store {StoreId} chưa đủ dữ liệu để đăng ký điểm lấy hàng.", store.Id);
            return false;
        }

        return await RegisterAsync(store, request, ct);
    }

    /// <summary>
    /// Gọi nhà vận chuyển đăng ký shop rồi lưu mã. Best-effort: lỗi chỉ được log, KHÔNG ném ra
    /// để không làm hỏng luồng gọi (thêm địa chỉ, tạo vận đơn…).
    /// </summary>
    private async Task<bool> RegisterAsync(GardenStore store, ShopRegistrationRequest request, CancellationToken ct)
    {
        try
        {
            var shopId = await _shipping.RegisterShopAsync(request, ct);
            if (shopId is not > 0)
            {
                _logger.LogWarning("[Shipping] {Provider} không cấp mã shop cho store {StoreId}.", _shipping.Name, store.Id);
                return false;
            }

            // UPDATE 1 cột, không qua change-tracker: hàm này chạy trong nhiều luồng đã tracked
            // sẵn entity store (vd StoreService.AddAddressAsync) nên Update() sẽ đụng key trùng.
            await _uow.Stores.SetCarrierShopIdAsync(store.Id, shopId.Value, ct);
            store.GhnShopId = shopId;   // đồng bộ vào instance caller đang cầm
            _logger.LogInformation("[Shipping] Store {StoreId} được cấp mã shop {ShopId} ({Provider}).",
                store.Id, shopId, _shipping.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Shipping] Đăng ký điểm lấy hàng cho store {StoreId} thất bại.", store.Id);
            return false;
        }
    }

    public async Task<CarrierShopSyncResultResponse> BackfillAsync(int batchSize, CancellationToken ct = default)
    {
        // Filter ở SQL đã loại store thiếu địa chỉ/mã vùng; ở đây lọc nốt điều kiện SĐT (phải
        // chuẩn hoá nên không dịch được sang SQL). Kết quả: chỉ store đủ điều kiện mới gửi request.
        var stores = await _uow.Stores.GetMissingCarrierShopIdAsync(batchSize, ct);
        var result = new CarrierShopSyncResultResponse { Scanned = stores.Count };

        foreach (var store in stores)
        {
            ct.ThrowIfCancellationRequested();

            var registration = BuildRegistration(store);
            if (registration is null)
            {
                result.Skipped.Add(Skip(store, StoreShippingIssueCodes.PickupPhoneInvalid,
                    ApiStatusMessages.StoreShipping.PickupPhoneInvalid));
                continue;
            }

            if (await RegisterAsync(store, registration, ct))
                result.Provisioned++;
            else
                result.Skipped.Add(Skip(store, StoreShippingIssueCodes.CarrierRegistrationFailed,
                    ApiStatusMessages.StoreShipping.CarrierRegistrationFailed));
        }

        return result;
    }

    private static CarrierShopSyncSkippedResponse Skip(GardenStore store, string reason, string message)
        => new() { StoreId = store.Id, StoreName = store.Name, Reason = reason, Message = message };

    /// <summary>
    /// Dựng payload đăng ký từ dữ liệu store. Trả null khi còn thiếu trường bắt buộc —
    /// cùng bộ điều kiện với <see cref="StoreShippingReadiness"/> nên hai bên không lệch nhau.
    /// </summary>
    private static ShopRegistrationRequest? BuildRegistration(GardenStore store)
    {
        var address = store.Address;
        if (address is null || string.IsNullOrWhiteSpace(address.StreetAddress)) return null;

        var districtId = address.Ward?.District?.GhnDistrictId;
        var wardCode = address.Ward?.GhnWardCode;
        if (districtId is null || string.IsNullOrEmpty(wardCode)) return null;

        // Cùng thứ tự ưu tiên với ShipmentRequestBuilder: SenderPhone → Hotline.
        var phone = VietnamPhone.IsCarrierValid(address.SenderPhone)
            ? VietnamPhone.Normalize(address.SenderPhone)
            : VietnamPhone.IsCarrierValid(store.Hotline) ? VietnamPhone.Normalize(store.Hotline) : null;
        if (phone is null) return null;

        var name = string.IsNullOrWhiteSpace(address.SenderName) ? store.Name : address.SenderName;
        if (string.IsNullOrWhiteSpace(name)) return null;

        return new ShopRegistrationRequest(name, phone, address.StreetAddress, districtId.Value, wardCode);
    }
}
