using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Validation;
using FengDeskAI.Application.Features.Shipping.DTOs;
using FengDeskAI.Domain.Entities.Vendor;

namespace FengDeskAI.Application.Features.Shipping.Services;

/// <summary>
/// Kiểm tra một cửa hàng đã đủ thông tin để tạo vận đơn chưa (điểm lấy hàng, mã vùng nhà vận chuyển,
/// người gửi + SĐT hợp lệ, mã shop). Chạy TRƯỚC khi gọi provider để lỗi hiện ra dưới dạng nghiệp vụ
/// rõ ràng thay vì 400 khó hiểu từ GHN. Xem docs/adr/fix-ghn-create-shipment.md §D.
/// </summary>
public static class StoreShippingReadiness
{
    /// <summary>
    /// Liệt kê thông tin còn thiếu. Rỗng = sẵn sàng tạo vận đơn.
    /// <paramref name="store"/> phải được nạp kèm <c>Address.Ward.District</c>
    /// (dùng <c>IStoreRepository.GetWithAddressByIdsAsync</c>).
    /// </summary>
    public static List<StoreShippingIssueResponse> Evaluate(GardenStore? store, bool requiresStoreShopId)
    {
        var issues = new List<StoreShippingIssueResponse>();
        if (store is null) return issues;

        var address = store.Address;
        if (address is null || string.IsNullOrWhiteSpace(address.StreetAddress))
        {
            issues.Add(Issue(StoreShippingIssueCodes.PickupAddressMissing,
                ApiStatusMessages.StoreShipping.PickupAddressField,
                ApiStatusMessages.StoreShipping.PickupAddressMissing,
                StoreShippingSections.StoreAddress));
        }
        else if (string.IsNullOrEmpty(address.Ward?.GhnWardCode) || address.Ward?.District?.GhnDistrictId is null)
        {
            // Địa chỉ có nhưng phường/xã chưa map sang mã vùng GHN → chọn lại phường/xã đã đồng bộ.
            issues.Add(Issue(StoreShippingIssueCodes.PickupWardGhnCodeMissing,
                ApiStatusMessages.StoreShipping.PickupWardField,
                ApiStatusMessages.StoreShipping.PickupWardGhnCodeMissing,
                StoreShippingSections.StoreAddress));
        }

        // SĐT gửi: ưu tiên SenderPhone, fallback hotline store. Hotline có thể là 1900/số cố định
        // → không dùng được cho nhà vận chuyển, khi đó bắt buộc nhập SenderPhone.
        if (!VietnamPhone.IsCarrierValid(address?.SenderPhone) && !VietnamPhone.IsCarrierValid(store.Hotline))
        {
            issues.Add(Issue(StoreShippingIssueCodes.PickupPhoneInvalid,
                ApiStatusMessages.StoreShipping.SenderPhoneField,
                ApiStatusMessages.StoreShipping.PickupPhoneInvalid,
                StoreShippingSections.StoreAddress));
        }

        if (string.IsNullOrWhiteSpace(address?.SenderName) && string.IsNullOrWhiteSpace(store.Name))
        {
            issues.Add(Issue(StoreShippingIssueCodes.PickupNameMissing,
                ApiStatusMessages.StoreShipping.SenderNameField,
                ApiStatusMessages.StoreShipping.PickupNameMissing,
                StoreShippingSections.StoreAddress));
        }

        if (requiresStoreShopId && store.GhnShopId is null or <= 0)
        {
            issues.Add(Issue(StoreShippingIssueCodes.CarrierShopIdMissing,
                ApiStatusMessages.StoreShipping.ShopIdField,
                ApiStatusMessages.StoreShipping.CarrierShopIdMissing,
                StoreShippingSections.StoreProfile));
        }

        return issues;
    }

    /// <summary>Gộp tên các trường thiếu thành chuỗi để nhét vào message. VD "Địa chỉ lấy hàng, Số điện thoại người gửi".</summary>
    public static string DescribeFields(IEnumerable<StoreShippingIssueResponse> issues)
        => string.Join(", ", issues.Select(i => i.Field));

    /// <summary>Message chặn thao tác, khác nhau giữa người tự sửa được (owner/admin) và garden staff.</summary>
    public static string BuildBlockedMessage(IEnumerable<StoreShippingIssueResponse> issues, bool canFix)
        => string.Format(
            canFix ? ApiStatusMessages.StoreShipping.OwnerBlockedFormat : ApiStatusMessages.StoreShipping.StaffBlockedFormat,
            DescribeFields(issues));

    private static StoreShippingIssueResponse Issue(string code, string field, string message, string section)
        => new() { Code = code, Field = field, Message = message, Section = section };
}
