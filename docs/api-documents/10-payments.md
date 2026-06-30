# 10 — Payments

[← Mục lục](./README.md)

Controller: `PaymentsController` · Route gốc: `/api/payments` · Mặc định `[Authorize]`; webhook là `[AllowAnonymous]`.

Thanh toán đơn hàng qua **PayOS**. Tạo link / xem trạng thái / hủy theo order của user đang đăng nhập.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| POST | `/api/payments/{orderId}` | Authenticated | Tạo link thanh toán PayOS |
| GET | `/api/payments/{orderId}` | Authenticated | Trạng thái thanh toán |
| POST | `/api/payments/{orderId}/cancel` | Authenticated | Hủy thanh toán |
| POST | `/api/payments/payos/webhook` | Public (verify chữ ký) | Webhook PayOS |
| POST | `/api/payments/{orderId}/dev/mark-paid` | Authenticated (**chỉ Dev**) | Giả lập thanh toán thành công |

---

## POST `/api/payments/{orderId}`

Tạo link thanh toán PayOS cho order `Pending`.
**Response `data`** = `CreatePaymentResponse`:
```json
{
  "orderId": "guid", "orderCode": 123456, "amount": 350000,
  "checkoutUrl": "https://pay.payos.vn/...", "qrCode": "data:image/...",
  "paymentLinkId": "...", "status": "Pending"
}
```

## GET `/api/payments/{orderId}`

**Response `data`** = `PaymentStatusResponse`:
```json
{
  "orderId": "guid", "orderStatus": "Paid", "orderCode": 123456,
  "paymentStatus": "Paid", "amount": 350000,
  "providerTransactionId": "...", "paidAt": "..."
}
```

## POST `/api/payments/{orderId}/cancel`

Hủy link PayOS + chuyển order/deliveries/transaction sang `Cancelled` + hoàn kho. Body (`CancelPaymentRequest`, tùy chọn):
```json
{ "reason": "Đổi ý" }
```

## POST `/api/payments/payos/webhook`

🔓 Public — PayOS gọi về khi trạng thái thay đổi. Verify chữ ký trong service. Body = JSON thô của PayOS.

## POST `/api/payments/{orderId}/dev/mark-paid`

⚠️ **Chỉ Development.** Giả lập thanh toán thành công (bỏ qua PayOS) để test luồng sau thanh toán: order→`Paid/Processing`, tạo delivery + shipment. Production trả `404`.

---

[← Orders](./09-orders.md) · [Tiếp: Returns →](./11-returns.md)
