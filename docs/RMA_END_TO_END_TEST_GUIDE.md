# Kịch bản test thủ công RMA end-to-end

Tài liệu này kiểm tra toàn bộ luồng trả hàng, hoàn tiền và đổi hàng theo đúng phân quyền hiện tại:

- Customer: tạo yêu cầu, bổ sung bằng chứng, hủy và theo dõi.
- Garden Owner/vendor: ghi nhận hoặc phản đối, xác nhận thực tế đã nhận hàng.
- Staff nền tảng: tiếp nhận, yêu cầu thêm bằng chứng, từ chối, duyệt hoàn tiền hoặc đổi hàng.
- Manager/Admin: xác nhận giao dịch hoàn tiền thủ công khi refund ở `ManagerReview`.

Không có bước ship-back tích hợp hãng vận chuyển. Customer và cửa hàng tự thỏa thuận cách bàn giao; hệ thống chỉ ghi nhận việc cửa hàng đã thực tế nhận hàng.

## 1. Điều kiện và tài khoản test

Chạy backend bằng environment `Development`. Tuyệt đối không thực hiện các bước giả lập dưới đây trên production.

Chuẩn bị tối thiểu bốn tài khoản:

| Ký hiệu | Role | Mục đích |
|---|---|---|
| C1 | Customer | Đặt hàng và tạo return |
| V1 | GardenOwner hoặc thành viên được quản lý store | Phản hồi phía cửa hàng |
| S1 | Staff | Trung gian ra quyết định RMA |
| M1 | Manager hoặc Admin | Xác nhận hoàn tiền thủ công |

Chuẩn bị sản phẩm:

- `P-A`: sản phẩm khách mua, tồn kho đủ.
- `P-B`: biến thể thay thế cùng cửa hàng, giá bằng hoặc thấp hơn `P-A`, còn tồn kho.
- `P-C`: biến thể thay thế cùng cửa hàng, ban đầu còn tồn; đưa tồn kho về 0 sau khi tạo yêu cầu để test fallback refund.
- Ít nhất hai ảnh JPG/PNG dưới 5 MB.

Nên tạo riêng từng order/delivery cho từng test case. Không dùng lại một return đã vào trạng thái terminal vì `Completed`, `Cancelled`, `Rejected` không thể chuyển tiếp.

## 2. Thiết lập order hợp lệ mà không mất tiền PayOS

### 2.1 Tạo order

Đăng nhập C1, thêm sản phẩm vào giỏ và checkout bình thường. Ghi lại `orderId` từ response hoặc URL chi tiết đơn.

### 2.2 Giả lập thanh toán thành công

Không bấm hoặc quét QR PayOS. Dùng JWT của một tài khoản đã đăng nhập và gọi:

```http
POST /api/payments/{orderId}/dev/mark-paid
Authorization: Bearer {token}
```

Kỳ vọng:

- HTTP 200.
- Payment thành công.
- Order đi vào luồng Paid/Processing.
- Delivery được tạo.
- Không phát sinh giao dịch tiền thật qua PayOS.

Nếu nhận 404, kiểm tra backend có đang chạy `ASPNETCORE_ENVIRONMENT=Development` hay không.

### 2.3 Giả lập giao hàng thành công

Cách ngắn nhất:

```http
POST /api/dev/deliveries/orders/{orderId}/delivered
Authorization: Bearer {token}
```

Endpoint tự tạo delivery nếu chưa có, sau đó đi qua `Confirmed → Shipped → Delivered`.

Kỳ vọng:

- HTTP 200.
- Tất cả delivery của order là `Delivered`.
- Order rollup thành `Completed`.
- Có `deliveredAt`.
- Trong trang chi tiết đơn của C1 xuất hiện nút tạo yêu cầu trả/đổi.

Có thể lấy delivery bằng:

```http
GET /api/dev/deliveries/orders/{orderId}
Authorization: Bearer {token}
```

## 3. Ma trận trạng thái chuẩn

### Hàng vật lý

```text
Requested
  ├─ Customer cancel ───────────────────────────────> Cancelled
  ├─ Staff request evidence ─> NeedMoreEvidence
  │                              └─ Customer resubmit ─> Requested
  └─ Staff accept ─> UnderReview ─> ReturnInTransit
                                        └─ Vendor confirm received
                                             ─> ItemReceived ─> Reviewing
                                                                  ├─ Reject
                                                                  ├─ Refunding
                                                                  └─ Exchanging
```

### Lý do PlantHealth

```text
Requested ─> Staff accept ─> UnderReview ─> Reviewing
```

Nhánh này bỏ qua bàn giao hàng và nút “Xác nhận đã nhận hàng”.

### Refund

```text
Reviewing ─> Staff duyệt hoàn tiền ─> Refunding
Refund: ManagerReview ─> Manager xác nhận thủ công ─> Completed
```

### Exchange

```text
Reviewing ─> Staff duyệt đổi hàng ─> Exchanging
  ├─ Còn hàng ─> tạo replacement delivery ─> Delivered ─> Completed
  └─ Hết hàng ─> fallback Refunding ─> ManagerReview ─> Completed
```

## 4. Bộ dữ liệu test đề xuất

Chuẩn bị các order độc lập sau:

| Case | Type | Reason | Mục đích |
|---|---|---|---|
| R1 | Refund | WrongItem | Happy path hoàn tiền vật lý |
| R2 | Refund | DamagedPackage | Yêu cầu bổ sung bằng chứng |
| R3 | Refund | NotAsDescribed | Staff từ chối |
| R4 | Refund | WrongItem | Customer hủy |
| R5 | Refund | PlantHealth | Không thu hồi hàng |
| E1 | Exchange | WrongItem | Đổi hàng còn tồn kho |
| E2 | Exchange | WrongItem | Hết hàng thay thế, fallback refund |
| V1 | Refund | WrongItem | Vendor ghi nhận |
| V2 | Refund | WrongItem | Vendor phản đối |

## 5. Test Customer

### TC-C01 — Mở form tạo yêu cầu

1. Đăng nhập C1.
2. Vào `Hồ sơ → Đơn hàng → Chi tiết đơn` của order đã Delivered.
3. Bấm nút trả hàng/đổi hàng.

Kiểm tra:

- Modal mở đúng delivery.
- Danh sách chỉ chứa item thuộc delivery đó.
- Có lựa chọn Refund/Exchange, lý do, số lượng, ảnh bằng chứng.
- Đóng modal không tạo dữ liệu.

### TC-C02 — Validation form

Thử lần lượt:

- Không chọn sản phẩm.
- Số lượng bằng 0 hoặc vượt số lượng đã mua.
- Không tải ảnh.
- Tải file không phải ảnh.
- Tải ảnh lớn hơn 5 MB.
- Chọn Exchange nhưng không chọn biến thể thay thế.
- Chọn biến thể khác cửa hàng.
- Chọn biến thể có tổng giá cao hơn hàng trả.

Kỳ vọng: frontend chặn hoặc backend trả 400 với thông báo tương ứng; không tạo ticket rác.

### TC-C03 — Tạo Refund

1. Chọn Refund.
2. Chọn item và số lượng hợp lệ.
3. Chọn `WrongItem`.
4. Nhập mô tả và tải ít nhất một ảnh.
5. Bấm xác nhận.

Kỳ vọng:

- Tạo thành công, trạng thái `Requested`.
- Xuất hiện trong `/profile/returns`.
- Chi tiết hiển thị đúng item, số lượng, tiền hoàn, ảnh và status log.
- Chỉ xuất hiện nút hủy khi còn `Requested`.

Với order COD và yêu cầu có hoàn tiền, lặp lại một lần để kiểm tra bắt buộc tên chủ tài khoản, số tài khoản và ngân hàng.

### TC-C03B — Tạo Exchange

1. Mở form trên một order Delivered khác.
2. Chọn `Exchange`.
3. Chọn item cần đổi.
4. Tại phần “Sản phẩm thay thế”, chọn biến thể cho từng item.
5. Kiểm tra tên biến thể, giá và tồn kho hiển thị đúng.
6. Chọn lý do, thêm mô tả và ảnh rồi xác nhận.

Kỳ vọng:

- Không chọn biến thể thay thế: frontend chặn.
- Biến thể hiện tại không xuất hiện như một lựa chọn thay thế.
- Backend từ chối nếu biến thể không tồn tại, khác cửa hàng hoặc đắt hơn hàng trả.
- Hợp lệ: ticket Exchange được tạo ở `Requested` và mỗi item lưu đúng `exchangeProductItemId`.

### TC-C04 — Hủy request

Dùng R4:

1. Tại `/profile/returns`, bấm “Hủy yêu cầu”.
2. Bấm “Không/Đóng” và xác nhận modal đóng, trạng thái không đổi.
3. Mở lại và bấm xác nhận hủy.

Kỳ vọng:

- Trạng thái `Cancelled`.
- Nút hủy biến mất.
- Refresh trang vẫn giữ `Cancelled`.
- Gọi lại API cancel phải trả 409.

### TC-C05 — Bổ sung bằng chứng

Dùng R2 sau khi Staff đã yêu cầu thêm bằng chứng:

1. Customer mở danh sách return.
2. Kiểm tra trạng thái `NeedMoreEvidence` và nút bổ sung bằng chứng.
3. Mở modal; thử xác nhận khi chưa chọn ảnh.
4. Thử thêm/xóa ảnh trong vùng preview.
5. Gửi ảnh hợp lệ.

Kỳ vọng:

- Không ảnh: frontend báo lỗi.
- Gửi thành công: ticket quay lại `Requested`.
- Ảnh mới và log mới xuất hiện trong chi tiết.
- Staff có thể tiếp nhận lại ticket.

### TC-C06 — Hướng dẫn bàn giao

Sau khi Staff accept một request hàng vật lý:

- Customer thấy “Chờ cửa hàng nhận hàng”.
- Chi tiết hiển thị hướng dẫn liên hệ cửa hàng.
- Không có ô mã vận đơn hoặc nút ship-back.
- Customer không có nút xác nhận cửa hàng đã nhận.

## 6. Test Staff nền tảng

Đăng nhập S1 và vào `/manager/order-returns`.

### TC-S01 — Danh sách, tab, tìm kiếm và phân trang

Kiểm tra lần lượt:

- Tab Tất cả.
- Tab yêu cầu mới.
- Tab chờ nhận hàng trả.
- Tab đang xử lý.
- Tab hoàn tất.
- Tìm bằng return ID, order ID và delivery ID.
- Nút trang trước/sau.
- Bấm dòng và nút “Chi tiết”.

Kỳ vọng: dữ liệu, badge trạng thái, số tiền, loại Refund/Exchange và tổng bản ghi nhất quán.

### TC-S02 — Yêu cầu thêm bằng chứng

Dùng R2 ở `Requested`:

1. Bấm “Yêu cầu thêm”.
2. Thử deadline không hợp lệ nếu UI cho nhập.
3. Nhập ghi chú cụ thể và deadline hợp lệ.
4. Xác nhận.

Kỳ vọng:

- Trạng thái `NeedMoreEvidence`.
- Customer nhận thông báo và thấy nội dung yêu cầu.
- Nút duyệt/từ chối không còn hiển thị sai trạng thái.
- Gọi lại API cùng trạng thái phải trả 409.

### TC-S03 — Tiếp nhận hàng vật lý

Dùng R1:

1. Ở `Requested`, bấm “Đồng ý/Tiếp nhận”.
2. Thử nút hủy modal trước.
3. Mở lại và xác nhận.

Kỳ vọng:

- Ticket đi qua `UnderReview` và dừng ở `ReturnInTransit`.
- Customer và vendor nhận thông báo.
- Customer không còn hủy được.
- Staff chưa được duyệt hoàn tiền khi hàng chưa được xác nhận nhận lại.

### TC-S04 — Tiếp nhận PlantHealth

Dùng R5:

1. Staff accept.
2. Refresh chi tiết.

Kỳ vọng:

- Ticket đi thẳng đến `Reviewing`.
- Không có bước `ReturnInTransit` hoặc `ItemReceived`.
- Vendor không cần bấm xác nhận nhận hàng.

### TC-S05 — Từ chối

Dùng R3:

1. Bấm từ chối.
2. Xác nhận khi lý do rỗng.
3. Nhập lý do rồi xác nhận.

Kỳ vọng:

- Rỗng: frontend chặn.
- Hợp lệ: trạng thái `Rejected`, lưu `rejectedReason` và status log.
- Customer nhìn thấy lý do.
- Không còn nút nghiệp vụ trên ticket terminal.

### TC-S06 — Duyệt hoàn tiền

Dùng R1 sau khi vendor đã confirm received và ticket là `Reviewing`:

1. Kiểm tra phần “Phản hồi từ cửa hàng”.
2. Bấm “Duyệt hoàn tiền”.
3. Bật/tắt “Nhập lại vào kho”.
4. Nhập ghi chú và xác nhận.

Kỳ vọng:

- Return thành `Refunding`.
- Refund được tạo và ở `ManagerReview` trong flow thủ công hiện tại.
- Nếu restock=true và không phải PlantHealth, tồn kho tăng đúng số lượng đúng một lần.
- Staff không thấy nút “Xác nhận hoàn tiền” dành cho Manager.
- Bấm/gọi approve lần hai trả 409, không cộng kho lần hai.

### TC-S07 — Duyệt đổi hàng còn tồn

Dùng E1 sau khi vendor confirm received:

1. Kiểm tra type là Exchange.
2. Bấm “Duyệt đổi hàng”.
3. Chọn restock và ghi chú.
4. Xác nhận.

Kỳ vọng:

- Return thành `Exchanging`.
- Có `replacementDeliveryId`/replacement delivery.
- Tồn kho sản phẩm thay thế giảm đúng số lượng.
- Nếu sản phẩm thay thế rẻ hơn, có refund phần chênh lệch.
- Không hiển thị nhầm nút “Duyệt hoàn tiền”.

Hoàn tất replacement delivery:

```http
POST /api/dev/deliveries/{replacementDeliveryId}/delivered
Authorization: Bearer {token}
```

Kỳ vọng return thành `Completed` khi giao thay thế thành công.

### TC-S08 — Đổi hàng hết tồn, fallback refund

Dùng E2, chọn P-C khi còn tồn để tạo yêu cầu. Trước khi Staff duyệt, Garden Owner cập nhật tồn kho P-C về 0; sau đó đi đến `Reviewing` và duyệt đổi hàng.

Kỳ vọng:

- Có log `Reviewing → Exchanging → Refunding`.
- Không tạo replacement delivery không hợp lệ.
- Refund được tạo với giá trị hàng trả.
- Manager tiếp tục xử lý như hoàn tiền thông thường.

## 7. Test Garden Owner/vendor

Đăng nhập V1, vào `/seller/{storeId}/returns`.

### TC-V01 — Phạm vi dữ liệu

- Chỉ thấy return thuộc store được phép quản lý.
- Không thấy return store khác.
- Mở chi tiết hiển thị đúng Customer, item, ảnh và lịch sử.
- Truy cập URL store khác phải bị frontend/backend từ chối.

### TC-V02 — Ghi nhận/đồng ý

Dùng V1 sau khi Staff accept:

1. Mở chi tiết.
2. Bấm “Ghi nhận / Đồng ý”.
3. Đóng modal để kiểm tra cancel.
4. Mở lại và xác nhận.

Kỳ vọng:

- `vendorResponse = Acknowledged`.
- Trạng thái return không bị vendor tự thay đổi.
- Các nút phản hồi biến mất sau khi đã phản hồi.
- Staff thấy “Cửa hàng đã ghi nhận / đồng ý”.

### TC-V03 — Phản đối

Dùng V2:

1. Bấm “Phản đối”.
2. Thử gửi lý do rỗng.
3. Nhập lý do và gửi.

Kỳ vọng:

- Rỗng: frontend chặn.
- `vendorResponse = Disputed`.
- Lý do xuất hiện trong status log.
- Return không tự chuyển `Rejected`.
- Staff vẫn có thể duyệt hoặc từ chối sau khi xem phản đối.

### TC-V04 — Xác nhận đã nhận hàng

Dùng request ở `ReturnInTransit`:

1. Bấm “Đã nhận hàng” từ danh sách.
2. Đóng modal; trạng thái không đổi.
3. Mở lại, đọc cảnh báo và bấm “Tôi đã nhận hàng”.

Kỳ vọng:

- Không yêu cầu tracking code.
- Return đi `ReturnInTransit → ItemReceived → Reviewing`.
- Có `receivedAt`.
- Nút xác nhận nhận hàng biến mất.
- Gọi lại phải trả 409; không restock tại bước này.

## 8. Test Manager/Admin hoàn tiền

Đăng nhập M1 và mở ticket `Refunding` tại `/manager/order-returns`.

### TC-M01 — Điều kiện hiển thị nút

- Refund `ManagerReview`: có nút “Xác nhận hoàn tiền”.
- Refund trạng thái khác: không có nút; UI hiển thị trạng thái hiện tại.
- Staff đăng nhập cùng trang: không thấy nút Manager.

### TC-M02 — Validation modal xác nhận

1. Mở modal xác nhận hoàn tiền.
2. Thử bỏ trống lý do.
3. Thử không tải bằng chứng.
4. Tải ảnh bằng chứng, sau đó bấm dấu X xóa ảnh.
5. Tải lại ảnh và nhập lý do.
6. Bấm hủy, mở lại và kiểm tra form reset.
7. Điền đầy đủ và xác nhận.

Kỳ vọng:

- Backend không chấp nhận khi thiếu `manualReason` hoặc `evidenceUrl`.
- Upload hợp lệ hiển thị preview.
- Thành công: Refund `Completed`, Return `Completed`.
- Customer thấy hoàn tất.
- Xác nhận lần hai trả 409 và không tạo tác động tài chính lần hai.

Lưu ý: bước này chỉ xác nhận một giao dịch chuyển khoản thủ công đã được thực hiện; không gọi PayOS để trừ tiền thật.

## 9. Test authorization trực tiếp bằng API

Dùng cùng một `returnId` và đổi token:

| Thao tác | Customer | Vendor đúng store | Staff | Manager/Admin |
|---|---:|---:|---:|---:|
| GET `/returns/{id}` | Chủ ticket | Có | Có | Có |
| GET `/returns/stores/{storeId}` | Không | Có | Có | Có |
| POST `/cancel` | Chủ ticket | Không | Không theo UI | Không theo UI |
| POST `/vendor-acknowledge` | 403 | Có | Không dùng theo UI | Có thể theo quyền backend nếu actor phù hợp |
| POST `/vendor-dispute` | 403 | Có | Không dùng theo UI | Có thể theo quyền backend nếu actor phù hợp |
| POST `/confirm-received` | 403 | Có | Không dùng theo UI | Chỉ khi actor được quyền tài nguyên |
| POST `/accept` | 403 | 403 | Có | Có |
| POST `/request-more-evidence` | 403 | 403 | Có | Có |
| POST `/approve-refund` | 403 | 403 | Có | Có |
| POST `/approve-exchange` | 403 | 403 | Có | Có |
| POST `/reject` | 403 | 403 | Có | Có |
| POST `/refunds/{id}/manager-confirm` | 403 | 403 | 403 | Có |

Ngoài 403, luôn kiểm tra 404 khi dùng ID không tồn tại và 409 khi đúng role nhưng sai trạng thái.

## 10. Regression và tính nhất quán

Sau mỗi happy path, kiểm tra:

- Refresh trình duyệt không làm mất trạng thái.
- Back/forward không gửi request lần hai.
- Double-click nút xác nhận không tạo hai refund, hai replacement delivery hoặc restock hai lần.
- Status log có đúng người thao tác, thời gian và ghi chú.
- Notification đến đúng Customer/vendor.
- Ticket terminal không còn nút thay đổi trạng thái.
- Tiền hoàn khớp `quantity × unitPrice` hoặc phần chênh lệch exchange.
- Không thể return quá số lượng còn có thể trả sau các return trước.
- Không thể tạo return ngoài return window.
- Không thể đọc hoặc thao tác return của Customer/store khác.

## 11. Mẫu ghi nhận kết quả

| Test case | Dữ liệu | Kết quả mong đợi | Kết quả thực tế | Pass/Fail | HTTP/API | Ảnh/log lỗi |
|---|---|---|---|---|---|---|
| TC-C01 | Order ... | Modal mở đúng |  |  |  |  |
| TC-S06 | Return ... | Refunding/ManagerReview |  |  |  |  |
| TC-V04 | Return ... | Reviewing + receivedAt |  |  |  |  |
| TC-M02 | Refund ... | Completed |  |  |  |  |

Khi gặp lỗi, lưu tối thiểu: tài khoản/role, URL, returnId, orderId, deliveryId, trạng thái trước, nút vừa bấm, request payload, HTTP status, response body và ảnh màn hình.
