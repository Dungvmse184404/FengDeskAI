# ARD — Refactor: AI tool-calling bằng handle ngắn + draft đơn hàng lưu DB

> **Status:** Proposal (chưa code).
> **Mục tiêu:** (1) model KHÔNG bao giờ nhìn thấy hay phải chép GUID; (2) draft đơn hàng sống trong DB,
> chịu được restart server, tự dọn sau khi đặt hoặc sau thời gian không dùng.
> **Không làm ở PR này:** đổi engine chấm điểm, đổi luồng thanh toán, bỏ `AiTextSanitizer`.

---

## 1. Hiện trạng — vì sao phải sửa

### 1.1 GUID đi thẳng vào context của model

Bốn tool đang serialize **nguyên DTO của service** ra cho LLM:

| Tool | Trả về | GUID lộ ra |
|---|---|---|
| `search_products` | `PagedResult<ProductResponse>.Items` | `product.id`, `items[].id` (variant), category/tag/store id |
| `list_my_orders` | `PagedResult<OrderResponse>.Items` | `order.id`, `orderItems[].id`, `deliveries[].id` |
| `list_my_workspaces` | `List<WorkspaceProfileResponse>` | `workspaceProfile.id` |
| `get_my_profile` | `UserResponse` | `user.id` |

Sáu tool nhận GUID làm tham số: `get_product(productId)`, `recommend_products(workspaceProfileId)`,
`get_payment_status(orderId)`, `prepare_order(productId, productItemId, shippingAddressId)`,
`confirm_order(draftId)`.

Hệ quả đang thấy:
- Model 4B chép sai/bịa GUID → tool trả "not found", hội thoại hỏng giữa chừng.
- GUID lọt vào thinking stream và câu trả lời → phải dựng cả `AiTextSanitizer` để đi vá.
- Payload tool result phình to (một `ProductResponse` đầy đủ ~15 field, phần lớn model không dùng)
  → tốn context window vốn đã hẹp.

### 1.2 Draft đơn hàng chỉ nằm trong RAM

`PrepareOrderTool` ghi `IMemoryCache` (`OrderDraftCacheKey.For` + `.Latest`), TTL 15'. Vấn đề:

- **Restart API là mất sạch** — user đang chờ xác nhận đơn thì draft biến mất.
- `AddDistributedMemoryCache` không thực sự distributed → scale nhiều instance là hỏng.
- Không audit được: không biết user đã prepare bao nhiêu lần, bỏ dở ở đâu.
- Không có gì dọn dẹp — dựa hoàn toàn vào TTL của cache.

### 1.3 Tool exchange không được lưu

`RunWithToolsAsync` dựng `messages` mới mỗi lượt từ `chat_messages` trong DB. Kết quả tool **không**
được lưu → sang lượt user nói "ok chốt", model đã quên `draftId`. Đó là lý do tồn tại pointer
`OrderDraftCacheKey.Latest` như một bản vá. Refactor này xử lý tận gốc: `confirm_order` không cần id nữa.

### 1.4 Ghi chú phát hiện thêm

`Tools/CancelOrderTool.cs` tồn tại nhưng **không được đăng ký trong `DependencyInjection.cs`** → code
chết, model không bao giờ gọi được. Quyết định trong PR này: đăng ký hoặc xóa (xem §7).

---

## 2. Thiết kế — handle ngắn

### 2.1 Định dạng

Mỗi thực thể model có thể nhắc tới được cấp một mã ngắn, đục (opaque), theo hội thoại:

```
P1, P2…   Product
V1, V2…   ProductItem (variant)
A1, A2…   UserAddress
O1, O2…   Order
W1, W2…   WorkspaceProfile
S1, S2…   GardenStore
```

Tiêu chí: ngắn (2–4 ký tự), model nhỏ chép không sai, và **không phải GUID** nên lộ ra ngoài cũng
không tiết lộ gì.

### 2.2 Phạm vi sống: theo HỘI THOẠI, không phải theo lượt

Handle phải sống qua nhiều lượt, nếu không sẽ vấp đúng cái bẫy sau: lượt N mint `P1..P5`, lượt N+1
mint lại `P1..P3` cho sản phẩm khác → nếu model lỡ dùng lại `P1` cũ nó sẽ **âm thầm trỏ sai sản phẩm**.
Đó là lỗi nguy hiểm hơn cả GUID sai (GUID sai thì 404, thấy ngay).

Vì vậy bảng handle là **append-only theo chatbox**, counter chỉ tăng: lượt sau mint tiếp `P6, P7…`.
Mint **idempotent** — cùng một entity trong cùng hội thoại luôn ra cùng handle (search 2 lần vẫn là `P1`).

Lưu ở đâu: `IMemoryCache`, key `ai-refs:{chatboxId}`, **sliding expiration 2 giờ**, cap ~500 entry mỗi
hội thoại (vượt thì loại entry cũ nhất).

> **Vì sao handle KHÔNG cần xuống DB nhưng draft thì cần:** handle hỏng là lỗi hồi phục được — trả
> lỗi có hướng dẫn, model gọi lại `search_products` là xong, user không mất gì. Draft hỏng là mất ý
> định mua hàng của user. Chỉ cái thứ hai đáng trả giá bằng một bảng DB.

### 2.3 API

`Application/Features/CustomerCare/Refs/`:

```csharp
public enum AiRefKind { Product, ProductItem, Address, Order, WorkspaceProfile, Store }

public interface IAiRefRegistry
{
    /// Cấp (hoặc lấy lại) handle cho entity trong hội thoại. Idempotent theo (conversation, kind, id).
    string Mint(string conversationKey, AiRefKind kind, Guid id);

    /// null = handle không tồn tại / sai loại / hội thoại đã hết hạn.
    Guid? Resolve(string conversationKey, AiRefKind kind, string? handle);
}
```

- `conversationKey` = `ctx.ChatboxId?.ToString() ?? $"user-{ctx.UserId}"` (luồng intake không có chatbox).
- Impl `AiRefRegistry` là **Singleton**, bọc `IMemoryCache`, lock theo từng object hội thoại.
- Resolve **không phân biệt hoa/thường**, tự trim, chấp nhận cả `p1` lẫn `P1`.
- Sai loại phải fail: `Resolve(conv, Product, "A3")` → null (không được lấy nhầm địa chỉ làm sản phẩm).

Helper cho tool:

```csharp
// ToolArgs bổ sung
public static string? GetRef(JsonElement e, string name);       // đọc chuỗi handle
public static string RefError(string param, string listTool);   // lỗi CHUẨN, có hướng dẫn tự sửa
```

`RefError` trả JSON dạng:
```json
{ "error": "Unknown reference 'P9' for 'productRef'.",
  "recover": "Call search_products again and use a code from its result. Never invent codes." }
```
Thông điệp phải dạy model tự phục hồi — đây là điểm khác biệt lớn so với "not found" hiện tại.

### 2.4 Hình dạng tool result mới

Nguyên tắc: **tool tự nặn shape gọn cho model, không serialize thẳng DTO của service nữa.**

`search_products` (ví dụ):
```json
{ "total": 12,
  "items": [
    { "ref": "P1", "name": "Cây Kim Tiền để bàn", "element": "Moc",
      "priceFrom": 150000, "inStock": true,
      "variants": [ { "ref": "V1", "name": "Chậu sứ trắng", "price": 150000, "stock": 8 } ] }
  ] }
```

Bỏ hẳn field `Link = "[name](/products/{guid})"` của `recommend_products` — xem §4.

---

## 3. Thay đổi từng tool

| Tool | Tham số cũ | Tham số mới | Result: thay gì |
|---|---|---|---|
| `search_products` | (không id) | giữ nguyên | shape gọn + `ref` cho product & variant |
| `get_product` | `productId` GUID | `productRef` | `ref`, variants có `ref` |
| `recommend_products` | `workspaceProfileId` GUID | `workspaceRef` | items có `ref`, **bỏ field `Link`** |
| `list_my_workspaces` | — | — | shape gọn + `ref` (thay vì dump DTO) |
| `list_my_addresses` | — | — | `id` → `ref` |
| `list_my_orders` | `limit` | giữ | shape gọn + `ref`, bỏ id của orderItem/delivery |
| `get_payment_status` | `orderId` GUID | `orderRef` | — |
| `get_my_profile` | — | — | bỏ field `id` khỏi output |
| `get_shop_info` | — | — | `ref` nếu có trả store id |
| `get_chat_partner_info` | — | — | rà, bỏ mọi id |
| `prepare_order` | `productId`, `productItemId`, `shippingAddressId` | `productRef`, `variantRef`, `addressRef` | **bỏ hẳn `draftId` khỏi output** |
| `confirm_order` | `draftId`, `paymentMethod` | **chỉ còn `paymentMethod`** | — |
| `compute_destiny_chart` | (không id) | giữ nguyên | — |

**`confirm_order` không còn tham số id là thay đổi quan trọng nhất của cả refactor.** Model không thể
chỉ định nhầm draft vì nó không được phép chỉ định gì cả — server tự lấy draft Pending mới nhất của
(user, chatbox). Pointer `OrderDraftCacheKey.Latest` bị xóa, cùng toàn bộ đoạn prompt dặn dò về `draftId`.

---

## 4. Hệ quả: link sản phẩm

Hiện prompt bắt model tự viết `[Tên](/products/{id})` — nghĩa là model **buộc phải** có GUID. Muốn giấu
GUID thì phải bỏ luật này, và link sẽ do BE chèn 100% qua `LinkifyProducts` (đã có sẵn, deterministic).

Cần gia cố `LinkifyProducts` vì giờ nó là cơ chế **duy nhất** tạo link:

1. So khớp không phân biệt dấu + hoa/thường (đã có `unaccent` phía DB, C#-side cần hàm tương đương).
2. Nếu model viết tên rút gọn ("Kim Tiền" thay vì "Cây Kim Tiền để bàn") thì hiện đang trượt →
   bổ sung khớp theo tiền tố/chuỗi con đủ dài (≥60% độ dài tên, ≥8 ký tự).
3. Tool result trả kèm `name` chính xác và prompt yêu cầu **viết đúng tên như tool trả về**.

Rủi ro cần chấp nhận: model paraphrase tên quá xa thì mất link. Đổi lại, không còn đường nào để GUID
rò ra câu trả lời. Nếu sau khi chạy thật thấy tỉ lệ mất link cao, phương án dự phòng là cho tool trả
`linkText` đã dựng sẵn dạng `[Tên](#P1)` rồi BE thay `#P1` bằng URL thật ở khâu hậu xử lý.

---

## 5. Draft đơn hàng xuống DB

### 5.1 Entity

`Domain/Entities/CustomerCare/AiOrderDraft.cs` (kế thừa `BaseEntity`), bảng `ai_order_drafts`:

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `user_id` | uuid, FK `users` | scope bắt buộc |
| `chatbox_id` | uuid?, FK `chatboxes` | null = ngoài luồng chat |
| `product_item_id` | uuid, FK `product_items` | v1 vẫn 1 sản phẩm/draft |
| `quantity` | int | |
| `unit_price_snapshot` | numeric(18,2) | để phát hiện đổi giá |
| `shipping_address_id` | uuid, FK `user_address` | |
| `status` | text (enum string) | `Pending / Confirmed / Expired / Cancelled` |
| `expires_at` | timestamptz | mặc định `now + 15'` |
| `confirmed_order_id` | uuid?, FK `orders` | audit + chống tạo đơn trùng |

Index:
- `ix_ai_order_drafts_user_chatbox_status` trên `(user_id, chatbox_id, status, expires_at DESC)` — truy vấn nóng của `confirm_order`.
- `ix_ai_order_drafts_status_expires` trên `(status, expires_at)` — worker dọn dẹp.

Enum lưu dạng string theo convention (`HasConversion<string>().HasMaxLength(16)`).
Config đặt ở `Infrastructure/Persistence/Configurations/AiOrderDraftConfiguration.cs`.
Migration: `AiOrderDraftPersistence`.

### 5.2 Repository

`Application/Interfaces/Repositories/IAiOrderDraftRepository.cs`:

```csharp
Task<AiOrderDraft?> GetLatestPendingAsync(Guid userId, Guid? chatboxId, DateTime now, CancellationToken ct);
Task AddAsync(AiOrderDraft draft, CancellationToken ct);
/// Chiếm draft theo kiểu atomic: UPDATE ... WHERE id=@id AND status='Pending'. Trả false nếu 0 dòng.
Task<bool> TryClaimAsync(Guid draftId, CancellationToken ct);
Task<int> SupersedePendingAsync(Guid userId, Guid? chatboxId, CancellationToken ct);
Task<int> ExpireOverdueAsync(DateTime now, CancellationToken ct);
Task<int> PurgeTerminalAsync(DateTime before, CancellationToken ct);
```

Thêm `IAiOrderDraftRepository AiOrderDrafts { get; }` vào `IUnitOfWork` + impl.

### 5.3 Vòng đời

```
prepare_order
  └─► SupersedePending(user, chatbox)     -> mọi draft Pending cũ = Cancelled
      INSERT draft Pending, expires_at = now + DraftTtlMinutes
      (KHÔNG trả draftId cho model)

confirm_order
  └─► GetLatestPending(user, chatbox, now)
      không có / hết hạn -> lỗi "no active draft, call prepare_order again"
      TryClaim(draftId)                    -> UPDATE ... WHERE status='Pending'
        0 dòng  -> đã có lượt khác chiếm  -> lỗi, KHÔNG tạo đơn
        1 dòng  -> status = Confirming
      re-validate giá/tồn kho (logic hiện có, giữ nguyên)
        lệch giá -> status = Cancelled, báo giá mới
      CheckoutAsync -> tạo đơn thật
        thành công -> status = Confirmed, confirmed_order_id = order.Id
        thất bại   -> status = Cancelled
```

**`TryClaimAsync` là bắt buộc, không phải tối ưu.** Bản cache hiện tại đạt "dùng 1 lần" nhờ
`_cache.Remove` trước khi checkout. Chuyển sang DB mà chỉ đọc rồi ghi thường sẽ mở ra race: hai lượt
confirm song song (user bấm 2 lần / job trùng) đọc cùng một draft Pending → **tạo 2 đơn hàng thật**.
Phải là một câu UPDATE có điều kiện, kiểm số dòng ảnh hưởng.

Thêm trạng thái trung gian `Confirming` để phân biệt "đang tạo đơn" với "đã xong" — nếu process chết
giữa chừng, worker sẽ quét `Confirming` quá hạn và cho về `Cancelled`.

> **Cần xác minh khi code:** `OrderService.CheckoutAsync` có tự mở transaction không. Nếu có, đừng bọc
> cả cụm trong `ExecuteInTransactionAsync` — nested transaction với Npgsql sẽ ném. Cập nhật draft nên
> là `SaveChangesAsync` riêng SAU khi checkout xong.

### 5.4 Worker dọn dẹp

`WebAPI/Workers/AiOrderDraftCleanupWorker.cs`, theo đúng khuôn `OrderExpirationWorker`
(`BackgroundService` + `PeriodicTimer` + `IOptionsMonitor` để bật/tắt qua appsettings, không cần restart):

```
mỗi ScanIntervalSeconds (mặc định 300):
  ExpireOverdueAsync(now)                       Pending/Confirming quá expires_at -> Expired
  PurgeTerminalAsync(now - PurgeAfterDays)      XÓA CỨNG các dòng đã terminal
```

Config `AiOrderDraft` trong appsettings:

```json
"AiOrderDraft": {
  "IsActive": true,
  "DraftTtlMinutes": 15,
  "ScanIntervalSeconds": 300,
  "PurgeAfterDays": 7
}
```

**Xóa cứng, không soft-delete.** Draft là dữ liệu tạm, giữ lại chỉ tổ phình bảng; 7 ngày là đủ để
điều tra khi có khiếu nại "tôi chốt rồi mà không thấy đơn". Đơn thật đã có `orders` làm bản ghi chính
thức nên không mất dấu vết gì.

### 5.5 Đáp ứng đúng yêu cầu ban đầu

- *"user reload thì AI vẫn nhớ"* — draft ở DB, không phụ thuộc RAM; `confirm_order` tra theo
  (user, chatbox) nên reload trang, đổi thiết bị, hay restart API đều tìm lại được.
- *"sau khi đã đặt sẽ xóa"* — `Confirmed` ngay khi checkout xong, worker purge sau `PurgeAfterDays`.
- *"sau 1 khoảng thời gian ko dùng sẽ xóa"* — `expires_at = now + 15'`; `prepare_order` mới sẽ
  supersede draft cũ nên không tồn đọng.

---

## 6. Đổi prompt (`AiChatService.CoreDirective`)

Đây là phần dễ bị bỏ sót nhất — sai prompt thì code đúng vẫn hỏng.

**Xóa:**
- `"ALWAYS hyperlink products using the exact format: [Product name](/products/{id}) based on the exact product ID from the tool result."`
- Toàn bộ đoạn `draftId` trong ORDERING PROTOCOL (3 câu: "uses the draftId that prepare_order returned…",
  "call confirm_order WITHOUT the draftId parameter…", "NEVER invent or guess an id…").
- Mọi chữ "GUID" / "id" còn sót trong mô tả tool (`Description` + `Parameters` của từng tool class).

**Thêm mục mới:**

```
## REFERENCE CODES
- Tool results label every item with a short reference code (P1, V2, A1, O3, W1).
- To act on an item, pass its code back EXACTLY as given. Codes are case-insensitive.
- NEVER invent, guess, or modify a code. If a tool rejects a code, call the listing tool again
  and use a fresh one.
- Codes are INTERNAL. Never show them to the user, never mention them in your reply.

## PRODUCT LINKS
- Write the product's name EXACTLY as the tool returned it. The system attaches the clickable
  link automatically. Do NOT write URLs or ids yourself.
```

**Sửa ORDERING PROTOCOL thành:**

```
- To place an order: call prepare_order with productRef (+ variantRef if the product has several
  variants, + addressRef if the user picked a non-default address).
- Read the returned summary back to the user IN FULL and ask them to confirm.
- Only after their NEXT message clearly agrees, call confirm_order. It takes NO id — the system
  already knows which draft is theirs. Never call it in the same turn you showed the summary.
```

**Cập nhật kèm:** `ToolFriendlyNotes` (nhãn tiếng Việt hiển thị lúc `calling_tool`) nếu có đổi tên tool.

---

## 7. Dọn dẹp đi kèm

1. **`CancelOrderTool`** — chưa đăng ký DI. Quyết: đăng ký (nếu muốn AI hủy đơn được) hoặc xóa file.
   Đề xuất **xóa**: hủy đơn là hành động không hoàn tác, để user tự làm ở trang đơn hàng an toàn hơn.
2. **`OrderDraft` record + `OrderDraftCacheKey`** trong `DTOs/` — xóa, thay bằng entity.
3. **`AiTextSanitizer`** — GIỮ. Bề mặt rò rỉ co lại rất nhiều nhưng vẫn còn: model có thể "nhớ" GUID
   từ lịch sử chat cũ (tin nhắn đã lưu trước refactor), và luồng workspace intake chưa đụng tới.
   Sau khi refactor chạy ổn 1–2 tuần, đánh giá lại xem có nên hạ mức lọc `LiveStream` không.
4. Hai attribute `data-drawer-interaction="message-bubble"` ở FE giờ là code chết — xóa.

---

## 8. Thứ tự thực hiện

**PR 1 — Handle (không đụng schema).**
`IAiRefRegistry` + impl + DI → `ToolArgs.GetRef/RefError` → sửa 12 tool (shape result + tham số) →
sửa `CoreDirective` → gia cố `LinkifyProducts`.
Sau PR này `prepare_order`/`confirm_order` vẫn dùng cache, chỉ đổi cách nhận tham số.

**PR 2 — Draft xuống DB.**
Entity + EF config + migration → repository + `IUnitOfWork` → sửa `PrepareOrderTool`/`ConfirmOrderTool`
→ worker dọn dẹp + config → xóa `OrderDraft` record cũ.

**PR 3 — Dọn dẹp** (§7) + cập nhật tài liệu (§10).

Tách 2 PR đầu vì PR 1 không có migration, rollback rẻ; PR 2 đụng schema nên cần review kỹ hơn và test
riêng phần race condition.

---

## 9. Kịch bản test (chưa có test tự động — làm thủ công)

**Handle:**
1. `search_products` → prepare bằng `ref` trả về → đặt hàng thành công.
2. Truyền handle bịa (`P99`) → nhận lỗi có `recover`, model tự gọi lại search, không hỏng hội thoại.
3. Sai loại: `prepare_order(productRef: "A1")` → phải lỗi, không được nhận nhầm địa chỉ.
4. Search 2 lần cùng sản phẩm → cùng một handle (idempotent).
5. Hội thoại dài qua nhiều lượt → counter tăng liên tục, handle cũ vẫn resolve đúng sản phẩm cũ.
6. Restart API giữa chừng → handle cũ mất → lỗi có hướng dẫn, không phải exception.
7. Đọc lại toàn bộ hội thoại: **không có GUID nào** trong tool result, thinking stream, câu trả lời.

**Draft:**
8. prepare → **reload trang** → "ok chốt" → đơn được tạo đúng (đây là yêu cầu chính).
9. prepare → **restart API** → "ok chốt" → vẫn tạo được đơn.
10. prepare → chờ quá `DraftTtlMinutes` → confirm → báo hết hạn, không tạo đơn.
11. prepare 2 lần liên tiếp → chỉ draft mới nhất còn Pending, cái cũ `Cancelled`.
12. **Race:** gọi `confirm_order` 2 lần đồng thời trên cùng draft → đúng **1** đơn được tạo.
13. Đổi giá sản phẩm giữa prepare và confirm → báo giá mới, draft `Cancelled`, không tạo đơn.
14. Worker: draft terminal quá `PurgeAfterDays` → biến mất khỏi bảng.
15. Tắt `AiOrderDraft:IsActive` → worker ngừng quét, không cần restart.

---

## 10. Tài liệu phải cập nhật sau khi merge

- `docs/api-documents/20-chat.md` — mô tả tool + tham số.
- `docs/api-documents/09-orders.md` — luồng đặt hàng qua AI.
- `docs/erd/SEP490_FengDeskAI.drawio` — thêm bảng `ai_order_drafts`.
- `ARCHITECTURE.md` §3.4 — thêm `AiOrderDraftCleanupWorker` vào danh sách background worker.
- `docs/adr/ai-order-tool-design.md` — ADR cũ mô tả draft trong cache, đánh dấu superseded bởi file này.
- `CLAUDE.md` mục "AI tools" — ghi rõ quy ước handle.

---

## 11. Rủi ro / điểm cần quyết

| # | Vấn đề | Đề xuất |
|---|---|---|
| 1 | Model nhỏ vẫn có thể đọc handle ra cho user ("bạn chọn P1 nhé") | Prompt cấm + cân nhắc regex `\b[PVAOWS]\d{1,3}\b` trên câu trả lời cuối để **cảnh báo log**, chưa nên tự động sửa (dễ bắt nhầm text hợp lệ) |
| 2 | `LinkifyProducts` thành cơ chế link duy nhất | Gia cố khớp tên (§4); theo dõi tỉ lệ mất link 1 tuần đầu; có phương án `#P1` dự phòng |
| 3 | Handle mất khi restart giữa hội thoại | Chấp nhận — lỗi hồi phục được. Nếu thực tế khó chịu thì chuyển registry sang `IDistributedCache` (đã có sẵn abstraction) |
| 4 | Nested transaction ở `confirm_order` | Xác minh `CheckoutAsync` trước khi code; cập nhật draft bằng `SaveChanges` riêng |
| 5 | Chat cũ trong DB vẫn chứa GUID, model đọc lại từ history | Giữ `AiTextSanitizer`; không migrate dữ liệu cũ |
| 6 | v1 chỉ 1 sản phẩm/draft | Giữ nguyên phạm vi. Schema đã tách bảng nên sau này thêm `ai_order_draft_items` không phá vỡ gì |
