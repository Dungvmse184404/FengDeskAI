namespace FengDeskAI.Application.Features.Shipping.DTOs;

/// <summary>Khu vực dữ liệu chứa thông tin còn thiếu — FE dùng để điều hướng owner đến đúng trang.</summary>
public static class StoreShippingSections
{
    /// <summary>Hồ sơ cửa hàng (tên, hotline, cấu hình nhà vận chuyển).</summary>
    public const string StoreProfile = "StoreProfile";
    /// <summary>Địa chỉ lấy hàng của cửa hàng (đường, phường/xã, người gửi, SĐT gửi).</summary>
    public const string StoreAddress = "StoreAddress";
}

/// <summary>Mã lỗi thiếu thông tin — FE map sang UI, KHÔNG parse message tiếng Việt.</summary>
public static class StoreShippingIssueCodes
{
    public const string PickupAddressMissing = "PICKUP_ADDRESS_MISSING";
    public const string PickupWardGhnCodeMissing = "PICKUP_WARD_GHN_CODE_MISSING";
    public const string PickupPhoneInvalid = "PICKUP_PHONE_INVALID";
    public const string PickupNameMissing = "PICKUP_NAME_MISSING";
    public const string CarrierShopIdMissing = "CARRIER_SHOP_ID_MISSING";
    /// <summary>Đủ dữ liệu nhưng nhà vận chuyển từ chối/không phản hồi khi đăng ký shop.</summary>
    public const string CarrierRegistrationFailed = "CARRIER_REGISTRATION_FAILED";
}

/// <summary>
/// Kết quả một lượt đồng bộ mã shop nhà vận chuyển (worker định kỳ hoặc nhân viên sàn bấm tay).
/// Chỉ tính các store ĐÃ có địa chỉ + mã vùng; store chưa khai địa chỉ không nằm trong phạm vi
/// đồng bộ vì đó là việc của chủ cửa hàng — xem qua endpoint readiness của từng store.
/// </summary>
public class CarrierShopSyncResultResponse
{
    /// <summary>Số store được xét trong lượt này (đã qua filter ở SQL).</summary>
    public int Scanned { get; set; }
    /// <summary>Số store được cấp mã shop thành công.</summary>
    public int Provisioned { get; set; }
    /// <summary>Store bị bỏ qua kèm lý do — để màn hình nhân viên sàn hiển thị việc cần xử lý.</summary>
    public List<CarrierShopSyncSkippedResponse> Skipped { get; set; } = new();
}

public class CarrierShopSyncSkippedResponse
{
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = null!;
    /// <summary>Mã lý do — dùng chung bộ mã với <see cref="StoreShippingIssueCodes"/>.</summary>
    public string Reason { get; set; } = null!;
    public string Message { get; set; } = null!;
}

/// <summary>Một mục thông tin còn thiếu của cửa hàng, chặn việc tạo vận đơn.</summary>
public class StoreShippingIssueResponse
{
    /// <summary>Mã ổn định để FE map sang UI (xem <see cref="StoreShippingIssueCodes"/>).</summary>
    public string Code { get; set; } = null!;
    /// <summary>Tên trường thiếu, hiển thị cho người dùng. VD "Số điện thoại người gửi".</summary>
    public string Field { get; set; } = null!;
    /// <summary>Mô tả đầy đủ + cách khắc phục.</summary>
    public string Message { get; set; } = null!;
    /// <summary>Khu vực cần sửa (xem <see cref="StoreShippingSections"/>) — FE điều hướng theo giá trị này.</summary>
    public string Section { get; set; } = null!;
}

/// <summary>
/// Tình trạng sẵn sàng tạo vận đơn của một cửa hàng, kèm ngữ cảnh vai trò người đang xem.
/// Owner/Admin tự sửa được (<see cref="CanFix"/> = true) → FE điều hướng sang <see cref="Section"/>;
/// garden staff chỉ được báo để liên hệ chủ cửa hàng.
/// </summary>
public class StoreShippingReadinessResponse
{
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = null!;
    /// <summary>True = đủ thông tin, bấm "Tạo đơn ship" được.</summary>
    public bool IsReady { get; set; }
    /// <summary>True nếu người đang xem là owner/admin — được phép tự bổ sung thông tin.</summary>
    public bool CanFix { get; set; }
    /// <summary>"Owner" | "Staff" | "Admin" — vai trò của người đang xem với cửa hàng này.</summary>
    public string Role { get; set; } = null!;
    /// <summary>Câu thông báo đã soạn sẵn theo vai trò — FE hiển thị trực tiếp.</summary>
    public string Message { get; set; } = null!;
    /// <summary>Khu vực đầu tiên cần bổ sung (null khi đã đủ, hoặc khi người xem không sửa được).</summary>
    public string? Section { get; set; }
    public List<StoreShippingIssueResponse> Issues { get; set; } = new();
}
