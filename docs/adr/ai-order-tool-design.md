# AI Order Tool — Thiết kế trước khi code

> Cho phép user đặt hàng ngay trong chatbox AI: "tôi muốn mua sản phẩm A" → AI chuẩn bị đơn,
> xin xác nhận, tạo đơn và đưa link thanh toán. **AI không bao giờ tự thanh toán.**

## 1. Ba nguyên tắc an toàn (không thương lượng)

1. **AI chỉ tạo draft** — `prepare_order` không ghi gì vào DB ngoài draft tạm (cache, TTL 15').
2. **Confirm cần lượt đồng ý riêng** — `confirm_order` chỉ nhận `draftId` do prepare sinh ra,
   không nhận productId trực tiếp → model không thể bịa đơn. AI phải đọc lại tóm tắt đơn và
   chờ user đồng ý rõ ràng ở lượt kế mới được gọi confirm.
3. **Thanh toán luôn qua link** — confirm tạo order pending + trả `CheckoutUrl` (PayOS),
   user tự bấm trả tiền. COD cũng phải được user chọn rõ, không mặc định.

## 2. Flow hội thoại

```
User: "mua Tượng Tỳ Hưu đồng"
AI  → prepare_order(productId, qty=1)
BE  : validate sản phẩm, resolve variant, check địa chỉ mặc định, preview phí ship
    → draft { draftId, summary, missing[] }

Nhánh A — thiếu thông tin (missing != rỗng):
AI  : "Bạn chưa có địa chỉ giao hàng, điền tại [đây](/profile/addresses) rồi quay lại nhé"
User: (điền xong) "xong rồi, tiếp tục đi"
AI  → prepare_order lại từ đầu (draft cũ bỏ)

Nhánh B — đủ thông tin:
AI  : đọc tóm tắt (tên, variant, số lượng, giá, phí ship, địa chỉ) + hỏi xác nhận
User: "ok chốt"
AI  → confirm_order(draftId)
BE  : re-validate giá/tồn kho → CheckoutAsync → CreatePaymentAsync
    → { orderId, checkoutUrl }
AI  : "Đơn đã tạo! Thanh toán tại [link](checkoutUrl), hết hạn sau 15 phút"
```

## 3. Spec 2 tools

### `prepare_order`
| Param | Type | Ghi chú |
|---|---|---|
| `productId` | string (GUID), required | Từ kết quả recommend/search trong hội thoại |
| `quantity` | integer | default 1, kẹp 1..10 |
| `productItemId` | string (GUID) | optional — khi user đã chọn variant cụ thể |

Xử lý BE:
- Resolve variant: sản phẩm 1 variant → tự chọn; nhiều variant mà không có `productItemId`
  → trả `missing: ["variant"]` kèm danh sách variant (id, tên, giá) để AI hỏi user.
- Check tồn kho, giá hiện tại, địa chỉ mặc định (`ShippingAddressId` null = mặc định).
- Gọi `PreviewShippingFeeAsync` lấy phí ship cho summary.
- Lưu draft vào `IMemoryCache` key `order-draft:{userId}:{draftId}`, TTL 15'.

Trả về (JSON cho LLM):
```json
{
  "draftId": "...",
  "summary": { "productName", "variant", "quantity", "unitPrice", "shippingFee", "total", "addressText" },
  "missing": [],            // hoặc ["address"] / ["variant"]
  "fixLinks": { "address": "/profile/addresses" },
  "note": "Read the summary back to the user and WAIT for explicit confirmation before calling confirm_order."
}
```

### `confirm_order`
| Param | Type | Ghi chú |
|---|---|---|
| `draftId` | string (GUID), required | Do prepare_order sinh ra |
| `paymentMethod` | string enum `PayOS\|COD` | default PayOS |

Xử lý BE:
- Lấy draft từ cache — không có/hết hạn → lỗi "draft expired, hãy prepare lại".
- **Re-validate giá + tồn kho**; lệch so với draft → không tạo đơn, trả diff để AI báo user.
- `CheckoutAsync(userId, new CheckoutRequest { Items = [...], ShippingAddressId, PaymentMethod })`.
- PayOS → gọi tiếp `CreatePaymentAsync(orderId, userId)` lấy `CheckoutUrl`.
- Xóa draft khỏi cache (idempotency: gọi lần 2 cùng draftId → lỗi rõ ràng, không tạo đơn đôi).

Trả về: `{ orderId, status, checkoutUrl?, expiresInMinutes: 15 }`.

## 4. Chặn rủi ro

| Rủi ro | Chốt chặn |
|---|---|
| Model bịa productId / đơn ảo | confirm chỉ nhận draftId; prepare validate productId tồn tại |
| Prompt injection ở phòng chung | 2 tool này **chỉ enable trong phòng riêng** (`SendAsync`); `RespondInRoomAsync` lọc bỏ qua `BuildToolSpecs` (thêm flag `AllowWriteTools` vào context hoặc filter theo tên) |
| "Mua đi" 2 lần → 2 đơn | draft xóa ngay khi confirm thành công; draftId dùng 1 lần |
| Giá đổi giữa prepare↔confirm | re-validate tại confirm, lệch thì từ chối |
| Model tự confirm không hỏi | rule trong CoreDirective + `note` trong tool result; chấp nhận rủi ro dư (model nhỏ) vì hậu quả tối đa = 1 đơn pending chưa trả tiền, user hủy được |
| AI hứa gọi tool rồi dừng | đã có nudge-retry sẵn trong `RunWithToolsAsync` |

## 5. File cần tạo / sửa

**Tạo mới** (`src/FengDeskAI.Application/Features/CustomerCare/`):
- `Tools/PrepareOrderTool.cs` — inject `IProductService`(hoặc repo), `IOrderService`, `IUserAddressRepository`(qua UoW), `IMemoryCache`
- `Tools/ConfirmOrderTool.cs` — inject `IOrderService`, `IPaymentService`, `IMemoryCache`
- `DTOs/OrderDraft.cs` — record draft lưu cache (items, giá snapshot, addressId, createdAt)

**Sửa:**
- `Application/DependencyInjection.cs` — đăng ký 2 `IAiTool` mới
- `AiChatService.cs`
  - `CoreDirective`: thêm section ORDERING PROTOCOL (đọc summary → chờ đồng ý → mới confirm; không bao giờ tự bịa draftId)
  - `BuildToolSpecs()`: nhận flag phòng riêng/chung → phòng chung loại `prepare_order`, `confirm_order`
  - `RespondInRoomAsync`: truyền flag phòng chung
- `IAiTool.cs` — (tùy chọn) `AiToolContext` thêm `bool IsPrivateRoom`

**Không sửa:** OrderService/PaymentService — dùng nguyên API có sẵn
(`CheckoutAsync`, `PreviewShippingFeeAsync`, `CreatePaymentAsync`).

## 6. Điểm cần lưu ý khi code

- `CheckoutRequest.Items` nhận **productItemId** (variant), KHÔNG phải productId —
  prepare_order bắt buộc resolve variant trước.
- `CheckoutRequest.Items` để trống = đặt cả giỏ → **luôn truyền Items tường minh**, không bao giờ để trống.
- Món trùng giỏ sẽ bị xóa khỏi giỏ sau đặt (hành vi có sẵn của Checkout) — chấp nhận.
- PayOS đơn quá 15' không trả tiền sẽ hết hạn — nhắc user trong câu trả lời AI.
- Tool result là JSON cho LLM: giữ field ngắn gọn, số đã format sẵn (tránh model tính sai).
- Draft dùng `IMemoryCache` là đủ cho v1 (mất khi restart — chấp nhận, user prepare lại).

## 7. Test checklist

- [ ] Mua SP 1 variant, đủ địa chỉ → đơn + link PayOS
- [ ] SP nhiều variant → AI hỏi chọn variant
- [ ] Chưa có địa chỉ → AI đưa link, điền xong quay lại prepare thành công
- [ ] Confirm draftId sai/hết hạn → lỗi thân thiện, không tạo đơn
- [ ] Confirm 2 lần liên tiếp → chỉ 1 đơn
- [ ] @AI ở phòng chung yêu cầu mua hộ → AI không có tool, từ chối lịch sự
- [ ] COD flow → đơn tạo không cần link thanh toán
- [ ] Giá đổi sau prepare → confirm từ chối, AI báo giá mới
