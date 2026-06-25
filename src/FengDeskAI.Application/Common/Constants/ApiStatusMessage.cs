namespace FengDeskAI.Application.Common.Constants;

/// <summary>
/// Message trả về client qua <c>ServiceResult</c> — gom về một nơi để tái sử dụng và đồng nhất.
/// Hằng số kết thúc bằng <c>Format</c> là chuỗi định dạng cho <see cref="string.Format(string, object?[])"/>.
/// </summary>
public static class ApiStatusMessages
{
    public static class Auth
    {
        public const string InvalidCredentials = "Email hoặc mật khẩu không đúng.";
        public const string AccountDisabled = "Tài khoản đã bị vô hiệu hóa.";
        public const string AccountUnavailable = "Tài khoản không khả dụng.";
        public const string InvalidRefreshToken = "Refresh token không hợp lệ hoặc đã hết hạn.";
        public const string UserNotFound = "Không tìm thấy người dùng.";
        public const string LoginSuccess = "Đăng nhập thành công.";
        public const string RefreshSuccess = "Làm mới token thành công.";
        public const string LoggedOut = "Đã đăng xuất.";
    }

    public static class Registration
    {
        public const string EmailInUse = "Email đã được sử dụng.";
        public const string PhoneInUse = "Số điện thoại đã được sử dụng.";
        public const string OtpSent = "Đã gửi mã OTP đến email. Vui lòng kiểm tra hộp thư.";
        public const string OtpIncorrect = "Mã OTP không đúng.";
        public const string OtpExpired = "Mã OTP đã hết hạn. Vui lòng yêu cầu gửi lại.";
        public const string OtpTooManyAttempts = "Bạn đã nhập sai quá nhiều lần. Vui lòng yêu cầu gửi lại.";
        public const string VerifySuccess = "Xác thực thành công.";
        public const string InvalidSession = "Phiên đăng ký không hợp lệ hoặc đã hết hạn. Vui lòng xác thực lại.";
        public const string RegisterSuccess = "Đăng ký thành công.";
    }

    public static class Address
    {
        public const string Created = "Địa chỉ đã được tạo.";
        public const string NotFound = "Địa chỉ không tồn tại.";
        public const string Unauthorized = "Không có quyền truy cập địa chỉ này.";
        public const string Updated = "Cập nhật địa chỉ thành công.";
        public const string SetDefault = "Đã đặt làm địa chỉ mặc định.";
        public const string Deleted = "Đã xóa địa chỉ.";
        public const string RecipientNameRequired = "Tên người nhận không được để trống.";
        public const string RecipientPhoneRequired = "Số điện thoại người nhận không được để trống.";
        public const string StreetRequired = "Địa chỉ chi tiết không được để trống.";
        public const string WardInvalid = "Phường/xã không hợp lệ.";
    }

    public static class WorkspaceProfile
    {
        public const string NotFound = "Không tìm thấy workspace profile.";
        public const string NoDefault = "Bạn chưa có workspace profile mặc định.";
        public const string NameRequired = "Tên không được để trống.";
        public const string SurfaceAreaInvalid = "Diện tích mặt bàn phải > 0.";
        public const string Created = "Tạo workspace profile thành công.";
        public const string Updated = "Cập nhật thành công.";
        public const string SetDefault = "Đã đặt làm mặc định.";
        public const string Deleted = "Đã xóa workspace profile.";
    }

    public static class Category
    {
        public const string NotFound = "Không tìm thấy danh mục.";
        public const string NameRequired = "Tên danh mục không được để trống.";
        public const string ParentNotFound = "Danh mục cha không tồn tại.";
        public const string SelfParent = "Danh mục không thể là cha của chính nó.";
        public const string Created = "Tạo danh mục thành công.";
        public const string Updated = "Cập nhật danh mục thành công.";
        public const string Deleted = "Đã xóa danh mục.";
    }

    public static class Tag
    {
        public const string NotFound = "Không tìm thấy tag.";
        public const string NameRequired = "Tên tag không được để trống.";
        public const string AlreadyExists = "Tag đã tồn tại.";
        public const string NameTaken = "Tên tag đã được dùng.";
        public const string Created = "Tạo tag thành công.";
        public const string Updated = "Cập nhật tag thành công.";
        public const string Deleted = "Đã xóa tag.";
    }

    public static class Product
    {
        public const string NotFound = "Không tìm thấy sản phẩm.";
        public const string NameRequired = "Tên sản phẩm không được để trống.";
        public const string CreateForbidden = "Bạn không có quyền tạo sản phẩm cho cửa hàng này.";
        public const string UpdateForbidden = "Bạn không có quyền sửa sản phẩm này.";
        public const string DeleteForbidden = "Bạn không có quyền xóa sản phẩm này.";
        public const string ManageForbidden = "Bạn không có quyền thao tác sản phẩm này.";
        public const string Created = "Tạo sản phẩm thành công.";
        public const string Updated = "Cập nhật sản phẩm thành công.";
        public const string Deleted = "Đã xóa sản phẩm.";
        public const string CategoriesNotExist = "Có danh mục không tồn tại.";
        public const string CategoriesUpdated = "Cập nhật danh mục sản phẩm thành công.";

        public const string PriceInvalid = "Giá không hợp lệ.";
        public const string ItemNotFound = "Không tìm thấy biến thể.";
        public const string ItemCreated = "Thêm biến thể thành công.";
        public const string ItemUpdated = "Cập nhật biến thể thành công.";
        public const string ItemDeleted = "Đã xóa biến thể.";

        public const string ImageUrlRequired = "URL ảnh không được để trống.";
        public const string ImageFileRequired = "Vui lòng chọn tệp ảnh để tải lên.";
        public const string ImageTypeInvalid = "Chỉ chấp nhận ảnh JPG, PNG, BMP hoặc GIF.";
        public const string ImageNotFound = "Không tìm thấy ảnh.";
        public const string ImageCreated = "Thêm ảnh thành công.";
        public const string ImageDeleted = "Đã xóa ảnh.";
        public const string StylesNotExist = "Có mã phong cách (style) không tồn tại.";
        public const string VibesNotExist = "Có mã vibe không tồn tại.";

        // Model 3D
        public const string Model3DSourceImageRequired = "Sản phẩm chưa có ảnh để sinh model 3D.";
        public const string Model3DSourceImageNotFound = "Không tìm thấy ảnh nguồn đã chọn.";
        public const string Model3DAlreadyProcessing = "Model 3D đang được xử lý, vui lòng đợi hoàn tất.";
        public const string Model3DNotFound = "Sản phẩm chưa có model 3D.";
        public const string Model3DStarted = "Đã gửi yêu cầu sinh model 3D, đang xử lý nền.";
        public const string Model3DDeleted = "Đã xóa model 3D.";
        public const string Model3DProviderError = "Dịch vụ sinh 3D gặp lỗi, vui lòng thử lại sau.";
    }

    public static class Cart
    {
        public const string QuantityInvalid = "Số lượng phải lớn hơn 0.";
        public const string ProductUnavailable = "Sản phẩm không tồn tại hoặc ngừng bán.";
        public const string Empty = "Giỏ hàng trống.";
        public const string ItemNotFound = "Không tìm thấy dòng hàng trong giỏ.";
        public const string Cleared = "Đã xóa toàn bộ giỏ hàng.";
        /// <summary>{0} = số lượng tồn còn lại.</summary>
        public const string OutOfStockFormat = "Không đủ tồn kho (còn {0}).";
    }

    public static class Order
    {
        public const string NotFound = "Không tìm thấy đơn hàng.";
        public const string ShippingAddressInvalid = "Địa chỉ giao hàng không hợp lệ.";
        public const string NoShippingAddress = "Chưa có địa chỉ giao hàng. Vui lòng thêm địa chỉ hoặc đặt một địa chỉ mặc định.";
        public const string QuantityInvalid = "Số lượng phải lớn hơn 0.";
        public const string SomeProductsNotExist = "Một số sản phẩm không tồn tại.";
        public const string CartEmpty = "Giỏ hàng trống.";
        public const string NoProductsSelected = "Chưa chọn sản phẩm nào để đặt.";
        public const string PaymentMethodInvalid = "Phương thức thanh toán không hợp lệ.";
        public const string SomeProductsDiscontinued = "Có sản phẩm đã ngừng bán. Vui lòng kiểm tra lại.";
        public const string CancelOnlyPending = "Chỉ có thể hủy đơn ở trạng thái chờ xử lý.";
        public const string CancelPaidNotAllowed = "Đơn đã thanh toán, không thể hủy trực tiếp.";
        public const string ViewStoreDeliveryForbidden = "Bạn không có quyền xem đơn giao của cửa hàng này.";
        public const string DeliveryNotFound = "Không tìm thấy đơn giao.";
        public const string UpdateDeliveryForbidden = "Bạn không có quyền cập nhật đơn giao này.";
        public const string DeliveryStatusUpdated = "Cập nhật trạng thái giao hàng thành công.";
        /// <summary>{0} = tên sản phẩm, {1} = số lượng tồn còn lại.</summary>
        public const string ProductOutOfStockFormat = "Sản phẩm '{0}' không đủ tồn kho (còn {1}).";
        /// <summary>{0} = trạng thái hiện tại, {1} = trạng thái muốn chuyển.</summary>
        public const string DeliveryStatusTransitionFormat = "Không thể chuyển trạng thái từ {0} sang {1}.";
    }

    public static class Payment
    {
        public const string OrderNotFound = "Không tìm thấy đơn hàng.";
        public const string OrderNotPending = "Đơn hàng không ở trạng thái chờ thanh toán.";
        public const string CodNoOnlinePayment = "Đơn COD thanh toán khi nhận hàng, không cần thanh toán online.";
        public const string OrderAlreadyPaid = "Đơn hàng đã được thanh toán.";
        public const string AmountInvalid = "Số tiền thanh toán không hợp lệ.";
        public const string GatewayUnavailable = "Không tạo được link thanh toán. Thử lại sau.";
        public const string LinkCreated = "Đã tạo link thanh toán.";
        public const string WebhookInvalid = "Webhook không hợp lệ.";
        public const string WebhookNoMatch = "Đã nhận webhook (không khớp giao dịch).";
        public const string TransactionAlreadyProcessed = "Giao dịch đã được xử lý trước đó.";
        public const string WebhookProcessed = "Đã xử lý webhook thanh toán.";
        public const string CancelOnlyPending = "Chỉ hủy được khi đơn đang chờ thanh toán.";
        public const string NoTransactionToCancel = "Đơn chưa có giao dịch thanh toán để hủy.";
        public const string PaidCannotCancel = "Đơn đã thanh toán, không thể hủy thanh toán.";
        public const string PaymentCancelled = "Đã hủy thanh toán và hủy đơn hàng.";
        public const string MarkPaidNotPending = "Đơn không ở trạng thái chờ thanh toán.";
        public const string MarkPaidAlreadyPaid = "Đơn đã thanh toán.";
        public const string MarkPaidSuccess = "Đã giả lập thanh toán thành công (DEV).";
    }

    public static class Shipping
    {
        public const string WebhookUnmatched = "Đã nhận webhook nhưng chưa khớp delivery (lưu để đối soát sau).";
        public const string WebhookProcessed = "Đã xử lý webhook và cập nhật trạng thái giao hàng.";
        public const string DeliveryNotFound = "Không tìm thấy delivery.";
        public const string ViewProgressForbidden = "Bạn không có quyền xem tiến trình giao hàng này.";
        /// <summary>{0} = trạng thái delivery hiện tại.</summary>
        public const string WebhookInvalidStatusFormat = "Trạng thái webhook không hợp lệ với delivery hiện tại ({0}).";
    }

    public static class Store
    {
        public const string NotFound = "Không tìm thấy cửa hàng.";
        public const string NameRequired = "Tên cửa hàng không được để trống.";
        public const string HotlineRequired = "Hotline không được để trống.";
        public const string OwnerNotFound = "Chủ cửa hàng (owner) không tồn tại.";
        public const string EditForbidden = "Bạn không có quyền sửa cửa hàng này.";
        public const string DeleteForbidden = "Bạn không có quyền xóa cửa hàng này.";
        public const string Created = "Tạo cửa hàng thành công.";
        public const string Updated = "Cập nhật cửa hàng thành công.";
        public const string Deleted = "Đã xóa cửa hàng.";
        public const string HardDeleted = "Đã xóa vĩnh viễn cửa hàng.";
        public const string HardDeleteConflict = "Không thể xóa vĩnh viễn vì cửa hàng còn dữ liệu liên quan (sản phẩm/đơn hàng).";

        // ===== Owner (đồng sở hữu) =====
        public const string ManageOwnersForbidden = "Bạn không có quyền quản lý chủ sở hữu cửa hàng này.";
        public const string AlreadyOwner = "Người dùng đã là chủ sở hữu cửa hàng này.";
        public const string OwnerAdded = "Đã thêm chủ sở hữu.";
        public const string OwnerRemoved = "Đã gỡ chủ sở hữu.";
        public const string OwnerNotInStore = "Người dùng không phải chủ sở hữu cửa hàng này.";
        public const string CannotRemoveLastPrimary = "Không thể gỡ chủ sở hữu chính cuối cùng của cửa hàng.";
    }

    public static class StoreAddress
    {
        public const string StreetRequired = "Địa chỉ chi tiết không được để trống.";
        public const string WardInvalid = "Phường/xã không hợp lệ.";
        public const string AlreadyExists = "Cửa hàng đã có địa chỉ. Hãy dùng cập nhật địa chỉ.";
        public const string StoreHasNoAddress = "Cửa hàng chưa có địa chỉ. Hãy thêm địa chỉ trước.";
        public const string NotFoundForStore = "Cửa hàng chưa có địa chỉ.";
        public const string NotFound = "Không tìm thấy địa chỉ cửa hàng.";
        public const string Created = "Thêm địa chỉ cửa hàng thành công.";
        public const string Updated = "Cập nhật địa chỉ cửa hàng thành công.";
        public const string Deleted = "Đã xóa địa chỉ cửa hàng.";
        public const string HardDeleted = "Đã xóa vĩnh viễn địa chỉ cửa hàng.";
    }

    public static class Staff
    {
        public const string StoreNotFound = "Không tìm thấy cửa hàng.";
        public const string ViewForbidden = "Bạn không có quyền xem nhân viên cửa hàng này.";
        public const string AssignForbidden = "Bạn không có quyền phân công nhân viên cho cửa hàng này.";
        public const string UnassignForbidden = "Bạn không có quyền gỡ phân công nhân viên cửa hàng này.";
        public const string StaffNotFound = "Nhân viên (user) không tồn tại.";
        public const string AlreadyAssigned = "Nhân viên đã được phân công cho cửa hàng này.";
        public const string Assigned = "Phân công nhân viên thành công.";
        public const string AssignmentNotFound = "Không tìm thấy phân công đang hiệu lực.";
        public const string Unassigned = "Đã gỡ phân công nhân viên.";
    }

    public static class Returns
    {
        public const string NotFound = "Không tìm thấy yêu cầu trả hàng.";
        public const string DeliveryNotFound = "Không tìm thấy đơn giao để trả hàng.";
        public const string NotDelivered = "Chỉ có thể trả hàng sau khi đơn giao đã được giao thành công.";
        public const string OutsideWindow = "Đã quá thời hạn yêu cầu trả hàng/đổi trả.";
        public const string NoItems = "Vui lòng chọn ít nhất một sản phẩm để trả.";
        public const string ItemNotInDelivery = "Có sản phẩm không thuộc đơn giao này.";
        public const string QuantityInvalid = "Số lượng trả phải lớn hơn 0.";
        /// <summary>{0} = tên sản phẩm, {1} = số lượng còn có thể trả.</summary>
        public const string QuantityExceededFormat = "Sản phẩm '{0}' vượt quá số lượng có thể trả (còn {1}).";
        public const string BankInfoRequired = "Đơn COD cần thông tin tài khoản ngân hàng để hoàn tiền.";
        public const string ExchangeItemRequired = "Đổi hàng cần chọn biến thể thay thế cho mỗi sản phẩm.";
        public const string ExchangeItemNotFound = "Không tìm thấy biến thể thay thế hợp lệ của cửa hàng.";
        public const string ExchangeMoreExpensive = "Biến thể đổi có giá cao hơn — vui lòng trả hàng rồi đặt đơn mới.";
        /// <summary>{0} = tên biến thể, {1} = số lượng tồn còn lại.</summary>
        public const string ExchangeOutOfStockFormat = "Biến thể đổi '{0}' không đủ tồn kho (còn {1}).";
        public const string ManageForbidden = "Bạn không có quyền xử lý yêu cầu trả hàng của cửa hàng này.";
        public const string ViewForbidden = "Bạn không có quyền xem yêu cầu trả hàng này.";
        /// <summary>{0} = trạng thái hiện tại, {1} = trạng thái muốn chuyển.</summary>
        public const string InvalidTransitionFormat = "Không thể chuyển trạng thái yêu cầu từ {0} sang {1}.";
        public const string CancelNotAllowed = "Chỉ có thể hủy yêu cầu khi đang chờ duyệt hoặc đã duyệt.";
        public const string NoRefundToComplete = "Yêu cầu này chưa có lệnh hoàn tiền để xác nhận.";

        public const string Created = "Đã gửi yêu cầu trả hàng/đổi trả.";
        public const string Approved = "Đã duyệt yêu cầu trả hàng.";
        public const string Rejected = "Đã từ chối yêu cầu trả hàng.";
        public const string ShippedBack = "Đã ghi nhận thông tin gửi hàng trả.";
        public const string Received = "Đã nhận hàng trả, đang xử lý.";
        public const string Resolved = "Đã xử lý yêu cầu trả hàng.";
        public const string RefundCompleted = "Đã hoàn tất hoàn tiền.";
        public const string Cancelled = "Đã hủy yêu cầu trả hàng.";
    }

    public static class Review
    {
        public const string NotFound = "Không tìm thấy đánh giá.";
        public const string Created = "Tạo đánh giá thành công.";
        public const string Updated = "Cập nhật đánh giá thành công.";
        public const string Deleted = "Đã xóa đánh giá.";
        public const string Unauthorized = "Bạn không có quyền thao tác đánh giá này.";
        public const string NotPurchased = "Bạn chưa mua sản phẩm này nên không thể đánh giá.";
        public const string ProductNotFound = "Không tìm thấy sản phẩm.";
        public const string AlreadyReviewed = "Bạn đã đánh giá sản phẩm này rồi.";
        public const string RatingInvalid = "Điểm đánh giá phải từ 1 đến 5.";
        public const string ContentRequired = "Nội dung đánh giá không được để trống.";
    }
}
