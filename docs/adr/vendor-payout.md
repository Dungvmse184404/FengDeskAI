# ADR — Đối soát & chi tiền cho nhà vườn (payout T+N)

**Trạng thái:** ĐANG TẮT (24/09/2026) — code còn, cờ `PayoutPolicy.CreditToBalanceEnabled = false`
**Ngày:** 2026-09-23

## 1. Bối cảnh

Chính sách sàn: tiền của một đơn chỉ được nhận sau vài ngày kể từ khi đơn hoàn thành — chốt **7 ngày**, bằng
cửa sổ đổi trả. Trước thay đổi này hệ thống **không có** luồng chi nào: `users.balance` tồn tại nhưng không
ai cộng vào, còn `Refund`/`VendorLiability` thì nhắc tới "trừ vào payout kế tiếp" trong khi payout đó chưa
tồn tại ở đâu trong code.

Hiện trạng liên quan:

| Thứ đã có | Ở đâu | Ghi chú |
|---|---|---|
| `Delivery.Status = Delivered`, `DeliveredAt` | `Domain/Entities/Sales/Delivery.cs` | mốc duy nhất để tính "đơn xong" |
| `VendorLiability` (Pending → Disputed → Settled/Waived) | `Domain/Entities/Payment` | nền tảng ứng hoàn cho khách rồi trừ lại vendor |
| `ReturnWorkflow.ReturnWindowDays = 7` | `Features/Returns/Services` | khách được mở ticket trong 7 ngày kể từ khi giao |
| Thống kê cửa hàng | `GET /stores/{id}/statistics` | nơi đầu tiên nói ra "tiền đang ở đâu" |

## 2. Quyết định (bước 1 — đã làm)

`PayoutPolicy.HoldDays = 7` — **bằng đúng cửa sổ đổi trả**, một nguồn sự thật duy nhất. Thống kê cửa hàng
tách tiền đã giao thành hai ngăn:

- `pendingClearanceValue` — đã giao, **chưa** qua hết khoảng giữ;
- `availableForPayoutValue` — đã qua, tức phần có thể yêu cầu chi;
- `outstandingLiabilityValue` — công nợ chưa `Waived`, sẽ trừ vào kỳ chi ⇒ **thực nhận = available − outstanding**.

**Cộng tiền (bản đơn giản)**: `PayoutCreditService` chạy trong `ReturnSlaWorker` (sau bước auto-settle công
nợ) quét delivery `Delivered` đã quá hạn giữ và chưa cộng, rồi `users.balance += Subtotal` cho **chủ vườn
chính** (`garden_store_owners.is_primary`), đánh dấu `deliveries.payout_credited_at`.

Hai điểm phải giữ đúng nếu ai sửa:
- `payout_credited_at` là khoá chống cộng hai lần — worker quét lại mỗi chu kỳ, thiếu mốc là tiền nhân đôi;
- vườn nhiều đồng sở hữu thì tiền vào **một** người (primary), không chia đều. Chia đều cần bảng phân bổ
  riêng — để dành cho bước sổ cái. Vườn không có chủ chính thì **bỏ qua, không đánh dấu**, để khi gắn lại
  chủ tiền vẫn vào.

Chưa đụng tới: sổ cái, lệnh rút, thông tin ngân hàng, trừ công nợ khỏi số dư.

## 3. Việc còn phải làm (bước 2 — chưa làm)

1. **Sổ cái** `garden_ledger_entries` (garden_id, delivery_id/liability_id, amount có dấu, type, available_at,
   payout_id?). Không cộng dồn on-the-fly như hiện tại nữa: tiền đã chi phải **khoá lại**, mà muốn khoá thì
   phải có dòng sổ, không thể suy ra từ `deliveries` mỗi lần gọi API.
2. **`payouts`** (garden_id, amount, status `Requested → Approved → Paid/Failed`, bank info, requested_by,
   approved_by, paid_at, provider_ref) + `payout_items` trỏ tới các dòng sổ đã gom.
3. **Endpoint**: `GET /stores/{id}/balance`, `POST /stores/{id}/payouts` (vendor yêu cầu),
   `POST /payouts/{id}/approve|reject` (Manager), webhook/đối soát ngân hàng.
4. **Thông tin ngân hàng của vendor** — chưa có trường nào; cần bảng riêng + xác minh.
5. **Worker** chuyển dòng sổ từ *held* sang *available* khi tới `available_at` (hoặc tính theo mốc như hiện
   tại — nhưng khi đã có sổ thì nên materialize để không phải quét lại toàn bộ delivery).
6. **Tương tác với đổi trả**: ticket mở ra khi tiền đã chi ⇒ ghi công nợ; khi tiền **chưa** chi ⇒ nên giữ
   luôn dòng sổ tương ứng thay vì tạo công nợ (rẻ hơn và không phải đi đòi).

## 3b. Vì sao tắt (24/09/2026)

Bản đơn giản này bị ba lỗi tiền thật, phát hiện khi rà trước lúc demo:

| # | Lỗi | Hậu quả |
|---|---|---|
| 1 | `availableForPayoutValue` không đọc `payout_credited_at` | tiền đã cộng vào số dư vẫn hiện "có thể rút" mãi — đếm hai lần |
| 2 | `VendorLiability` không trừ khỏi `balance` | đơn bị trả sau khi đã cộng ⇒ sàn ứng tiền cho khách rồi không thu lại được |
| 3 | không khoá, chỉ dựa `payout_credited_at` giữa các lượt quét | chạy ≥ 2 instance là cộng đôi |

Ngoài ra `balance` chưa có DTO/endpoint nào đọc, nên tiền cộng vào cũng không ai nhìn thấy; và worker đang
ăn ké cờ `ReturnSla:IsActive` — tắt SLA đổi trả là tắt luôn trả tiền.

Giao diện đã ẩn thẻ "Có thể rút" (Dashboard + tab thống kê cửa hàng), chỉ còn hiển thị doanh thu. Bật lại
**sau khi** có sổ cái ở bước 2, vì cả ba lỗi trên đều tan khi mỗi lần cộng/trừ là một dòng sổ có khoá duy
nhất theo `delivery_id`.

## 4. Đánh đổi đã biết

**Khoảng giữ = cửa sổ đổi trả (7 ngày).** Đổi lại việc dòng tiền nhà vườn chậm một tuần, sàn không rơi vào
cảnh đã chi rồi mới đi đòi qua `VendorLiability`. Nếu sau này cần nới cho vendor uy tín, hai hướng:
chi theo **tỉ lệ** (80% sau 3 ngày, 20% sau 7) hoặc theo **hồ sơ vendor**. Cả hai đều cần sổ cái trước.

**Công nợ chưa trừ khỏi số dư.** `outstandingLiabilityValue` mới chỉ hiển thị; số dư cộng vào vẫn là toàn bộ
`Subtotal`. Khi có sổ cái thì công nợ phải thành dòng sổ âm — hiện tại phải trừ tay lúc chi.

## 5. Kiểm thử

1. Đơn vừa giao hôm nay → nằm ở `pendingClearanceValue`, `availableForPayoutValue = 0` (STORE-30e), và
   worker **không** cộng vào số dư.
2. Store mới tinh → cả hai ngăn = 0, `payoutHoldDays > 0` (STORE-29).
3. (khi có bước 2) Chi tiền xong → dòng sổ bị khoá, gọi lại API không tính lại vào "có thể rút".
4. (khi có bước 2) Công nợ `Settled` → trừ đúng vào kỳ chi kế tiếp; `Waived` → không trừ.
