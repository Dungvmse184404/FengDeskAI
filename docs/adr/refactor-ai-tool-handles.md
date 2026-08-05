# ARD — Refactor: AI tool-calling bằng handle ngắn + draft đơn hàng lưu DB

> **Status:** Proposal (chưa code). Bản này đã qua một vòng đối chiếu với code thật — các ràng buộc
> từ interceptor / transaction / query filter ở §6.1 là **đã kiểm chứng**, không phải giả định.
> **Mục tiêu:** (1) model KHÔNG bao giờ nhìn thấy hay phải chép GUID; (2) draft đơn hàng sống trong
> DB, chịu được restart server, và **tự xóa cứng** sau khi đặt xong hoặc sau thời gian không dùng.
> **Không làm ở PR này:** đổi engine chấm điểm, đổi luồng thanh toán, bỏ `AiTextSanitizer`.

---

## 1. Hiện trạng — vì sao phải sửa

### 1.1 GUID đi thẳng vào context của model

Sáu tool đang serialize **nguyên DTO của service** ra cho LLM:

| Tool | Trả về | GUID lộ ra |
|---|---|---|
| `search_products` | `PagedResult<ProductResponse>.Items` | `product.id`, `items[].id` (variant), category/tag/store id |
| `list_my_orders` | `PagedResult<OrderResponse>.Items` | `order.id`, `orderItems[].id`, `deliveries[].id` |
| `list_my_workspaces` | `List<WorkspaceProfileResponse>` | `workspaceProfile.id` |
| `get_my_profile` | `UserResponse` | `user.id` |
| `get_shop_info` | products projection | `p.Id` của từng sản phẩm |
| `get_chat_partner_info` | `{ profile, workspaces, orders }` thô | **toàn bộ id + PII của một người KHÁC** |

Năm tool nhận GUID làm tham số: `get_product(productId)`, `recommend_products(workspaceProfileId)`,
`get_payment_status(orderId)`, `prepare_order(productId, productItemId, shippingAddressId)`,
`confirm_order(draftId)`.

Hệ quả đang thấy:
- Model 4B chép sai/bịa GUID → tool trả "not found", hội thoại hỏng giữa chừng.
- GUID lọt vào thinking stream và câu trả lời → phải dựng cả `AiTextSanitizer` để đi vá.
- Payload tool result phình to → tốn context window vốn đã hẹp.

### 1.2 Draft đơn hàng chỉ nằm trong RAM

`PrepareOrderTool` ghi `IMemoryCache` (`OrderDraftCacheKey.For` + `.Latest`), TTL 15'. Vấn đề:

- **Restart API là mất sạch** — user đang chờ xác nhận đơn thì draft biến mất.
- `AddDistributedMemoryCache` không thực sự distributed → scale nhiều instance là hỏng.
- Không có gì dọn dẹp — dựa hoàn toàn vào TTL của cache.

### 1.3 Tool exchange không được lưu

`RunWithToolsAsync` dựng `messages` mới mỗi lượt từ `chat_messages` trong DB; kết quả tool chỉ nằm
trong `List<AiChatMessage>` trong bộ nhớ (`AiChatService.cs:391`), **không** ghi DB. Sang lượt user nói
"ok chốt", model đã quên `draftId`. Đó là lý do tồn tại pointer `OrderDraftCacheKey.Latest` như một bản
vá. Refactor này xử lý tận gốc: `confirm_order` không cần id nữa.

### 1.4 Ghi chú phát hiện thêm

`Tools/CancelOrderTool.cs` tồn tại nhưng **không được đăng ký trong `DependencyInjection.cs`** (đã
kiểm: 0 lần xuất hiện) → code chết, model không bao giờ gọi được. Xem §9.

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
```

Tiêu chí: ngắn (2–4 ký tự), model nhỏ chép không sai, và **không phải GUID** nên lộ ra ngoài cũng
không tiết lộ gì.

### 2.2 Phạm vi sống: theo HỘI THOẠI, không phải theo lượt

Handle phải sống qua nhiều lượt, nếu không sẽ vấp đúng cái bẫy sau: lượt N mint `P1..P5`, lượt N+1
mint lại `P1..P3` cho sản phẩm khác → nếu model lỡ dùng lại `P1` cũ nó sẽ **âm thầm trỏ sai sản phẩm**.
Đó là lỗi nguy hiểm hơn cả GUID sai (GUID sai thì 404, thấy ngay).

Vì vậy bảng handle là **append-only theo chatbox**, counter chỉ tăng: lượt sau mint tiếp `P6, P7…`.
Mint **idempotent** — cùng một entity trong cùng hội thoại luôn ra cùng handle.

Lưu ở đâu: `IMemoryCache`, key `ai-refs:{chatboxId}`, **sliding expiration 2 giờ**, cap ~500 entry mỗi
hội thoại (vượt thì loại entry cũ nhất).

> **Vì sao handle KHÔNG cần xuống DB nhưng draft thì cần:** handle hỏng là lỗi hồi phục được — trả
> lỗi có hướng dẫn, model gọi lại `search_products` là xong, user không mất gì. Draft hỏng là mất ý
> định mua hàng của user. Chỉ cái thứ hai đáng trả giá bằng một bảng DB.

### 2.3 API

`Application/Features/CustomerCare/Refs/`:

```csharp
public enum AiRefKind { Product, ProductItem, Address, Order, WorkspaceProfile }

/// Kết quả resolve: id thật + user SỞ HỮU entity đó (xem §2.5).
public readonly record struct AiRef(Guid Id, Guid OwnerUserId);

public interface IAiRefRegistry
{
    /// Cấp (hoặc lấy lại) handle. Idempotent theo (conversation, kind, id).
    string Mint(string conversationKey, AiRefKind kind, Guid id, Guid ownerUserId);

    /// null = handle không tồn tại / sai loại / hội thoại đã hết hạn.
    AiRef? Resolve(string conversationKey, AiRefKind kind, string? handle);
}
```

- `conversationKey` = `ctx.ChatboxId?.ToString() ?? $"user-{ctx.UserId}"`.
- Impl `AiRefRegistry` là **Singleton**, bọc `IMemoryCache`, lock theo từng object hội thoại.
- Resolve **không phân biệt hoa/thường**, tự trim.
- Sai loại phải fail: `Resolve(conv, Product, "A3")` → null.

Helper cho tool:

```csharp
// ToolArgs bổ sung
public static string? GetRef(JsonElement e, string name);
public static string RefError(string param, string listTool);
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
      "description": "…", "priceFrom": 150000, "inStock": true,
      "variants": [ { "ref": "V1", "name": "Chậu sứ trắng", "price": 150000, "stock": 8 } ] }
  ] }
```

> **Cắt field phải cân nhắc, không cắt cho gọn.** Prompt bắt model lập luận theo "product's element
> and attributes", nên `description` / element phụ / style / vibe là **đầu vào của chất lượng tư vấn**,
> không phải rác. Quy tắc: bỏ mọi `id`, bỏ field chỉ FE dùng (ảnh, slug, timestamp), **giữ mọi field
> mang ngữ nghĩa phong thủy**. Chốt từng field lúc code, và so lại chất lượng câu trả lời trước/sau.

Nhánh "thiếu variant" của `prepare_order` cũng phải trả `ref` chứ không phải id:
```json
{ "summary": null, "missing": ["variant"],
  "variants": [ { "ref": "V3", "name": "Chậu sứ trắng", "price": 150000, "stock": 8 } ],
  "note": "Ask the user which variant they want, then call prepare_order again with variantRef set." }
```

### 2.5 Handle của người KHÁC — bắt buộc tách bạch

`get_chat_partner_info` là tool duy nhất trả dữ liệu của **một người khác** (khách hàng, cho nhân
viên hỗ trợ đọc). Nếu nó mint handle vào chung registry hội thoại, model phía nhân viên có thể cầm
`W1` của khách rồi truyền vào `recommend_products` — vốn scope theo `context.UserId`.

Đã kiểm: `RecommendationService` gọi `GetByIdForUserAsync(profileId, userId)` nên **fail an toàn**,
không rò dữ liệu. Nhưng nó tạo ngõ cụt khó hiểu: model vừa đọc được hồ sơ xong lại nhận "không tìm
thấy hồ sơ không gian", rồi loay hoay thử lại.

Vì vậy `Resolve` trả kèm `OwnerUserId`, và **mọi tool self-scoped phải kiểm** `OwnerUserId ==
context.UserId`, sai thì trả lỗi nói rõ bản chất:

```json
{ "error": "Reference 'W1' belongs to the customer you are chatting with, not to you.",
  "recover": "You can read their info, but you cannot run your own tools on it." }
```

---

## 3. Thay đổi từng tool

| Tool | Tham số cũ | Tham số mới | Result: thay gì |
|---|---|---|---|
| `search_products` | (không id) | giữ nguyên | shape gọn + `ref` cho product & variant |
| `get_product` | `productId` GUID | `productRef` | `ref`, variants có `ref` |
| `recommend_products` | `workspaceProfileId` GUID | `workspaceRef` | items có `ref`, **bỏ field `Link`** |
| `list_my_workspaces` | — | — | shape gọn + `ref` (thay vì dump DTO) |
| `list_my_addresses` | — | — | `id` → `ref` |
| `list_my_orders` | `limit` | giữ | shape gọn + `ref`; bỏ id của orderItem/delivery |
| `get_payment_status` | `orderId` GUID | `orderRef` | — |
| `get_my_profile` | — | — | bỏ field `id` khỏi output |
| `get_shop_info` | — | — | **xem §3.1** |
| `get_chat_partner_info` | — | — | **xem §3.2** |
| `prepare_order` | `productId`, `productItemId`, `shippingAddressId` | `productRef`, `variantRef`, `addressRef` | **bỏ hẳn `draftId` khỏi output** |
| `confirm_order` | `draftId`, `paymentMethod` | **chỉ còn `paymentMethod`** | **thêm `orderRef` — xem §3.3** |
| `compute_destiny_chart` | (không id) | giữ nguyên | — |

**`confirm_order` không còn tham số id là thay đổi quan trọng nhất của cả refactor.** Model không thể
chỉ định nhầm draft vì nó không được phép chỉ định gì cả — server tự lấy draft Pending mới nhất của
(user, chatbox). Pointer `OrderDraftCacheKey.Latest` bị xóa, cùng toàn bộ đoạn prompt về `draftId`.

### 3.1 `get_shop_info`

Bản trước của tài liệu này ghi sai ("`ref` nếu có trả store id"). Thực tế tool **không** trả store id,
nhưng **có** trả `p.Id` của từng sản phẩm trong shop (`GetShopInfoTool.cs:61`).

Sửa:
- `p.Id` → `ref` (kind `Product`). Đây là dữ liệu công khai nên `ownerUserId = context.UserId`, ai đọc
  cũng được phép hành động lên nó.
- **Đồng thời `context.Products.Add(...)` cho từng sản phẩm.** Hiện tool này KHÔNG làm, nghĩa là sản
  phẩm liệt kê từ shop chưa bao giờ được `LinkifyProducts` gắn link. Đây là lỗi có sẵn, không phải do
  refactor sinh ra, nhưng sau refactor thì nó thành lỗi *nhìn thấy được* (model không còn tự viết
  URL được nữa) → sửa trong cùng PR.

### 3.2 `get_chat_partner_info`

Đây là bề mặt GUID + PII **lớn nhất** trong toàn bộ tool, và là tool duy nhất chạm dữ liệu người khác.
Không được xử lý qua loa. Thay dump DTO bằng projection tường minh, và mint handle với
`ownerUserId = granterId` (không phải `context.UserId`) theo §2.5:

```json
{ "profile":    { "name": "…", "gender": "…", "dateOfBirth": "…", "birthTime": "…" },
  "workspaces": [ { "ref": "W7", "name": "Bàn làm việc", "element": "Moc", "direction": "…" } ],
  "orders":     [ { "ref": "O4", "code": "…", "status": "…", "total": 0, "placedAt": "…" } ] }
```

- Field nào bị consent chặn thì giữ nguyên cơ chế `Denied` hiện có.
- `phone` / `email` chỉ ra khi `ShareProfile` bật — và cân nhắc bỏ hẳn, vì nhân viên đã thấy chúng ở
  panel FE, model không cần chúng để tư vấn.
- Handle `W7` / `O4` ở trên **không** dùng được với tool self-scoped (§2.5).

### 3.3 `confirm_order` phải mint handle cho đơn vừa tạo

Không có bước này thì sau khi đặt hàng xong, user hỏi "thanh toán tới đâu rồi" mà model không có mã
nào để truyền vào `get_payment_status`. Kết quả trả về thêm:

```json
{ "orderRef": "O9", "status": "Pending", "expiresInMinutes": 15, "note": "…" }
```

`orderId` GUID **không** xuất hiện trong tool result nữa (nó vẫn đi vào block thanh toán — §5).

---

## 4. Hệ quả: link sản phẩm

Hiện prompt bắt model tự viết `[Tên](/products/{id})` — nghĩa là model **buộc phải** có GUID. Muốn
giấu GUID thì phải bỏ luật này, và link do BE chèn 100% qua `LinkifyProducts`.

Cần gia cố `LinkifyProducts` vì giờ nó là cơ chế **duy nhất** tạo link:

1. So khớp không phân biệt dấu + hoa/thường (DB đã có `unaccent`; C#-side cần hàm tương đương).
2. Tool result trả kèm `name` chính xác; prompt yêu cầu **viết đúng tên như tool trả về**.
3. Bổ sung khớp tên rút gọn — **nhưng phải rất dè dặt**.

> **Link SAI tệ hơn không có link.** "Cây Kim Tiền để bàn" và "Cây Kim Tiền mini" khớp tiền tố nhau;
> khớp mờ ẩu sẽ dẫn user tới đúng cái họ không hỏi. Quy tắc: chỉ khớp khi kết quả là **duy nhất**
> trong `ctx.Products` của lượt đó; từ 2 ứng viên trở lên thì **không** gắn link. Ngưỡng cụ thể (độ
> dài tối thiểu, tỉ lệ khớp) chốt lúc code bằng dữ liệu tên sản phẩm thật, đừng bịa số trước.

**Khoảng trống đã biết:** `ctx.Products` chỉ sống trong MỘT lượt. Model nhắc lại sản phẩm của lượt
trước mà không gọi lại tool thì không có link. Trước đây model tự viết URL nên che được; sau refactor
thì không. Chấp nhận ở v1 — cách chữa là prompt yêu cầu gọi lại `get_product` khi muốn dẫn link.

Nếu chạy thật thấy tỉ lệ mất link cao: phương án dự phòng là tool trả `linkText` dựng sẵn dạng
`[Tên](#P1)` rồi BE thay `#P1` bằng URL thật ở khâu hậu xử lý.

---

## 5. Block thanh toán `@@payment:` — ngoại lệ DUY NHẤT được phép chứa GUID

`AiChatService.AppendPaymentBlock` gắn `@@payment:{json}@@` vào cuối tin nhắn AI, chứa `orderId` +
`checkoutUrl`. FE (`extractPaymentBlock`) tách block ra để render card QR / nút thanh toán.

Block này **cố ý** nằm ngoài mọi luật giấu GUID, vì ba lý do:
- Nó do **hệ thống** sinh sau khi đã censor, không phải model viết ra.
- FE cần `orderId` thật để điều hướng; handle không dùng được ở đó.
- Người dùng không thấy nó dưới dạng chữ — FE cắt trước khi render Markdown.

Vì vậy:
1. **Ghi thẳng vào tài liệu và vào test** rằng đây là ngoại lệ. Test §11 phải nói "không có GUID nào
   trong tool result / thinking stream / **phần văn bản model tự viết**", chứ không phải "trong câu
   trả lời" — nếu không người test sẽ báo lỗi giả.
2. **Sửa một lỗ rò thật ở FE:** `extractPaymentBlock` khi `JSON.parse` thất bại đang
   `return { text: content }` — tức trả nguyên block thô, user sẽ thấy
   `@@payment:{"orderId":"1b90…"}@@` hiện thành chữ trong bong bóng chat. Phải **luôn cắt block khỏi
   text** kể cả khi parse lỗi (chỉ bỏ phần card, không bỏ việc cắt).
3. Giữ nguyên luật prompt cấm model tự chép lại link thanh toán.

---

## 6. Draft đơn hàng xuống DB

### 6.1 Ràng buộc đã kiểm chứng trong code — đọc trước khi viết repository

| # | Sự thật | Hệ quả bắt buộc |
|---|---|---|
| 1 | `AppDbContext.cs:179` biến `EntityState.Deleted` → `Modified` + `IsDeleted=true` | `Remove()` **không xóa cứng được**. Phải dùng `ExecuteDeleteAsync`. Tiền lệ: `StoreRepository.cs:242` |
| 2 | `ExecuteUpdate/ExecuteDelete` chạy SQL thẳng, bỏ qua interceptor | Phải **tự set `UpdatedAt`**. Tiền lệ + ghi chú sẵn có: `StoreRepository.cs:50` |
| 3 | `HasQueryFilter(x => !x.IsDeleted)` khai theo TỪNG config (51/61 file), không phải toàn cục | Config mới **phải tự khai**, nếu không draft đã soft-delete vẫn lọt vào `GetLatestPendingAsync` |
| 4 | `UnitOfWork.ExecuteInTransactionAsync` gọi `BeginTransactionAsync` **vô điều kiện**, và `OrderService.CheckoutAsync` đã dùng nó | **Không được** bọc luồng confirm trong một `ExecuteInTransactionAsync` nữa — Npgsql sẽ ném. Cập nhật draft là `ExecuteUpdate`/`ExecuteDelete` riêng, SAU khi checkout xong |

### 6.2 Entity

`Domain/Entities/CustomerCare/AiOrderDraft.cs` (kế thừa `BaseEntity`), bảng `ai_order_drafts`:

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `user_id` | uuid, FK `users` | scope bắt buộc |
| `chatbox_id` | uuid?, FK `chatboxes` | xem §6.6 |
| `product_item_id` | uuid, FK `product_items` | v1 vẫn 1 sản phẩm/draft |
| `quantity` | int | |
| `unit_price_snapshot` | **khớp đúng kiểu cột giá của `product_items`** | để phát hiện đổi giá — lệch kiểu là so sánh sai ở phần thập phân |
| `shipping_address_id` | uuid, FK `user_address` | |
| `status` | text (enum string, max 16) | **chỉ hai giá trị: `Pending`, `Confirming`** — xem §6.4 |
| `expires_at` | timestamptz | `now + DraftTtlMinutes` |

**Không có `Confirmed` / `Expired` / `Cancelled`, và không có `confirmed_order_id`.** Mọi nhánh kết
thúc đều là **dòng biến mất**. Bảng này chỉ chứa draft đang sống.

Index:
- `ix_ai_order_drafts_user_chatbox_status` trên `(user_id, chatbox_id, status, expires_at DESC)` — truy vấn nóng của `confirm_order`.
- `ix_ai_order_drafts_status_expires` trên `(status, expires_at)` — worker dọn dẹp.

Config ở `Infrastructure/Persistence/Configurations/AiOrderDraftConfiguration.cs` (nhớ `HasQueryFilter`
— §6.1 mục 3). Migration: `AiOrderDraftPersistence`.

### 6.3 Repository

`Application/Interfaces/Repositories/IAiOrderDraftRepository.cs` — thêm
`IAiOrderDraftRepository AiOrderDrafts { get; }` vào `IUnitOfWork`:

```csharp
Task<AiOrderDraft?> GetLatestPendingAsync(Guid userId, Guid chatboxId, DateTime now, CancellationToken ct);

Task AddAsync(AiOrderDraft draft, CancellationToken ct);

/// Chiếm draft theo kiểu atomic — ExecuteUpdate, TỰ set updated_at (§6.1 mục 2):
///   UPDATE … SET status='Confirming', updated_at=@now WHERE id=@id AND status='Pending'
/// false = 0 dòng ảnh hưởng = lượt khác đã chiếm.
Task<bool> TryClaimAsync(Guid draftId, DateTime now, CancellationToken ct);

/// Xóa CỨNG (ExecuteDelete — §6.1 mục 1) mọi draft Pending của phòng. Dùng khi prepare_order
/// tạo draft mới. KHÔNG đụng dòng Confirming (có thể đang giữa chừng checkout).
Task<int> DeletePendingAsync(Guid userId, Guid chatboxId, CancellationToken ct);

/// Xóa CỨNG một draft. Dùng ở MỌI nhánh kết thúc của confirm_order (thành công, lệch giá, lỗi).
Task<int> DeleteAsync(Guid draftId, CancellationToken ct);

/// Worker: xóa CỨNG Pending quá expires_at + Confirming kẹt quá confirmingGrace.
Task<int> PurgeStaleAsync(DateTime now, TimeSpan confirmingGrace, CancellationToken ct);
```

Cả 4 hàm ghi đều là `ExecuteUpdateAsync`/`ExecuteDeleteAsync`, **không** đi qua change tracker —
đó là điều kiện để atomic-claim đúng và để xóa được thật (§6.1).

### 6.4 Vòng đời — xóa cứng ở MỌI nhánh kết thúc

```
prepare_order
  └─► DELETE mọi draft Pending của (user, chatbox)      ← thay cho "đánh dấu Cancelled"
      INSERT draft Pending, expires_at = now + DraftTtlMinutes
      (KHÔNG trả draftId cho model)

confirm_order
  └─► GetLatestPending(user, chatbox, now)
      không có / hết hạn ──► lỗi "no active draft, call prepare_order again"
      TryClaim(draftId)   ──► UPDATE … SET status='Confirming', updated_at=now
                              WHERE id=@id AND status='Pending'
        0 dòng ──► lượt khác đã chiếm ──► lỗi, KHÔNG tạo đơn
        1 dòng ──► ta sở hữu draft này
      re-validate giá / tồn kho (logic hiện có, giữ nguyên)
        lệch giá ──► DELETE draft, báo giá mới, bảo user prepare lại
      CheckoutAsync  (tự mở transaction — §6.1 mục 4, KHÔNG bọc thêm)
        thành công ──► DELETE draft  +  mint orderRef (§3.3)
        thất bại   ──► DELETE draft, báo lỗi
```

**`TryClaim` là bắt buộc, không phải tối ưu.** Bản cache hiện tại đạt "dùng một lần" nhờ `_cache.Remove`
trước khi checkout. Chuyển sang DB mà chỉ đọc-rồi-ghi thường sẽ mở ra race: hai lượt confirm song song
đọc cùng một draft Pending → **tạo 2 đơn hàng thật**. Phải là một câu UPDATE có điều kiện, kiểm số
dòng ảnh hưởng.

`Confirming` tồn tại để phân biệt "đang tạo đơn" với "chưa ai đụng". Nếu process chết giữa chừng,
dòng kẹt ở `Confirming` và worker dọn (§6.5) — quan trọng là nó **không còn là `Pending`** nên không
thể bị confirm lần hai.

Nếu `DELETE` sau checkout thành công bị lỗi (DB trục trặc), dòng vẫn ở `Confirming` → worker xóa sau.
Không sinh đơn trùng vì trạng thái đã rời `Pending`.

### 6.5 Worker dọn dẹp

`WebAPI/Workers/AiOrderDraftCleanupWorker.cs`, theo đúng khuôn `OrderExpirationWorker`
(`BackgroundService` + `PeriodicTimer` + `IOptionsMonitor` để bật/tắt qua appsettings, không restart):

```
mỗi ScanIntervalSeconds:
  DELETE  status='Pending'    AND expires_at < now                     ← "lâu không đụng thì xóa"
  DELETE  status='Confirming' AND updated_at < now - ConfirmingGrace   ← dọn xác process chết
```

```json
"AiOrderDraft": {
  "IsActive": true,
  "DraftTtlMinutes": 15,
  "ScanIntervalSeconds": 300,
  "ConfirmingGraceMinutes": 5
}
```

`DraftTtlMinutes` tính từ lúc TẠO, không trượt theo hoạt động chat: một draft không có thao tác nào
để "đụng vào" ngoài prepare (tạo mới) và confirm (tiêu thụ). 15' cũng khớp cửa sổ giữ link PayOS.

**Đánh đổi đã chấp nhận:** xóa cứng ở mọi nhánh nghĩa là **không còn dấu vết trong DB** khi user khiếu
nại "tôi chốt rồi mà không thấy đơn". Bù lại bằng log có cấu trúc ở `confirm_order` (draftId,
productItemId, quantity, kết quả) — `AiChatService` vốn đã log mọi tool call. Đơn tạo thành công thì
`orders` vẫn là bản ghi chính thức, không mất gì.

### 6.6 Ngữ nghĩa `chatboxId = null`

`prepare_order` / `confirm_order` chỉ chạy ở phòng riêng nên `ChatboxId` luôn có giá trị. Nhưng chữ ký
`GetLatestPendingAsync(userId, Guid? chatboxId, …)` mời gọi truyền null. Chốt: **`chatboxId` là bắt
buộc** ở cả hai hàm tra cứu — đổi tham số thành `Guid` không nullable; cột vẫn nullable chỉ để phục vụ
luồng tương lai. Draft chuẩn bị ở phòng này **không** confirm được ở phòng khác.

### 6.7 Đáp ứng đúng yêu cầu ban đầu

- *"user reload thì AI vẫn nhớ"* — draft ở DB; `confirm_order` tra theo (user, chatbox) nên reload
  trang, đổi thiết bị, hay restart API đều tìm lại được.
- *"sau khi đã đặt thì xóa"* — `DELETE` ngay khi checkout xong, không chờ worker.
- *"sau một khoảng thời gian không dùng thì xóa"* — worker `DELETE` khi quá `expires_at`.
- Cả hai đều là **xóa cứng**.

---

## 7. Đổi prompt (`AiChatService.CoreDirective`)

Đây là phần dễ bị bỏ sót nhất — sai prompt thì code đúng vẫn hỏng.

**Xóa:**
- `"ALWAYS hyperlink products using the exact format: [Product name](/products/{id})…"`
- Toàn bộ đoạn `draftId` trong ORDERING PROTOCOL (3 câu).
- Mọi chữ "GUID" / "id" còn sót trong `Description` + `Parameters` của từng tool class.

**Thêm:**

```
## REFERENCE CODES
- Tool results label every item with a short reference code (P1, V2, A1, O3, W1).
- To act on an item, pass its code back EXACTLY as given. Codes are case-insensitive.
- NEVER invent, guess, or modify a code. If a tool rejects a code, call the listing tool again
  and use a fresh one.
- Codes are INTERNAL. Never show them to the user, never mention them in your reply.
- Codes from get_chat_partner_info belong to the CUSTOMER, not to you: you may read that data,
  but you cannot pass those codes into your own tools.

## PRODUCT LINKS
- Write the product's name EXACTLY as the tool returned it. The system attaches the clickable
  link automatically. Do NOT write URLs or ids yourself.
- To link a product you mentioned in an earlier turn, call get_product again first.
```

**Sửa ORDERING PROTOCOL thành:**

```
- To place an order: call prepare_order with productRef (+ variantRef if the product has several
  variants, + addressRef if the user picked a non-default address).
- Read the returned summary back to the user IN FULL and ask them to confirm.
- Only after their NEXT message clearly agrees, call confirm_order. It takes NO id — the system
  already knows which draft is theirs. Never call it in the same turn you showed the summary.
```

**Cập nhật kèm:** `ToolFriendlyNotes` nếu có đổi tên tool.

---

## 8. Rủi ro triển khai

| # | Vấn đề | Xử lý |
|---|---|---|
| 1 | **Đa instance**: registry handle nằm trong `IMemoryCache` → mint ở máy này, resolve ở máy kia là hỏng | Ghi rõ giả định **hiện chỉ chạy 1 instance**. Muốn scale-out thì chuyển registry sang `IDistributedCache` (abstraction đã có) — cùng lúc phải giải quyết SignalR backplane |
| 2 | **Thời điểm deploy PR 2**: draft đang trong cache bốc hơi, user đang chờ chốt nhận "không tìm thấy draft" | Deploy lúc vắng; chấp nhận |
| 3 | Model nhỏ vẫn có thể đọc handle ra cho user ("bạn chọn P1 nhé") | Prompt cấm. Thêm regex `\b[PVAOW]\d{1,3}\b` trên câu trả lời cuối chỉ để **ghi log cảnh báo**, KHÔNG tự sửa (dễ bắt nhầm text hợp lệ). Chốt sau khi có số liệu thật |
| 4 | Handle mất khi restart giữa hội thoại | Chấp nhận — lỗi hồi phục được nhờ `recover` trong RefError |
| 5 | Chat cũ trong DB vẫn chứa GUID, model đọc lại từ history | Giữ `AiTextSanitizer`; không migrate dữ liệu cũ |
| 6 | v1 chỉ 1 sản phẩm/draft | Giữ nguyên phạm vi. Schema tách bảng nên sau này thêm `ai_order_draft_items` không phá vỡ gì |
| 7 | Mất dấu vết draft sau khi xóa cứng | Log có cấu trúc ở `confirm_order` (§6.5) |

---

## 9. Dọn dẹp đi kèm

1. **`CancelOrderTool`** — chưa đăng ký DI. Đề xuất **xóa file**: hủy đơn là hành động không hoàn tác,
   để user tự làm ở trang đơn hàng an toàn hơn là giao cho model 4B.
2. **`OrderDraft` record + `OrderDraftCacheKey`** trong `DTOs/` — xóa, thay bằng entity.
3. **`AiTextSanitizer`** — GIỮ. Bề mặt rò rỉ co lại nhiều nhưng vẫn còn (lịch sử chat cũ, luồng
   workspace intake chưa đụng tới). **Tiêu chí để hạ mức lọc `LiveStream`:** khi log cảnh báo ở §8
   mục 3 và log của sanitizer cho thấy **0 lần bắt được GUID trong 1000 lượt chat liên tiếp**. Không
   có con số thì đừng đụng vào.
4. Hai attribute `data-drawer-interaction="message-bubble"` ở FE giờ là code chết — xóa.

---

## 10. Thứ tự thực hiện

**PR 1a — Hạ tầng handle.** `IAiRefRegistry` + impl + DI, `ToolArgs.GetRef/RefError`, áp cho 3 tool
đọc thuần: `search_products`, `get_product`, `list_my_addresses`. Prompt thêm mục REFERENCE CODES.
Chạy thử để xem model nhỏ có bám mã được không **trước khi** đổi hết.

**PR 1b — Phần tool còn lại.** 9 tool còn lại (gồm §3.1, §3.2, §2.5), gia cố `LinkifyProducts`, sửa
`extractPaymentBlock` ở FE (§5), gỡ luật hyperlink khỏi prompt.

**PR 2 — Draft xuống DB.** Entity + EF config + migration → repository + `IUnitOfWork` → sửa
`PrepareOrderTool`/`ConfirmOrderTool` → worker dọn dẹp + config → xóa `OrderDraft` record cũ.

**PR 3 — Dọn dẹp** (§9) + cập nhật tài liệu (§12).

> Tách PR 1 làm hai vì repo **không có test tự động nào**: sửa 12 tool + toàn bộ prompt trong một
> nhát mà hỏng thì hỏng cả trợ lý, và không có gì chỉ ra hỏng ở đâu. PR 1a đủ nhỏ để đánh giá được
> việc model có dùng handle đúng hay không.

---

## 11. Kịch bản test (thủ công — repo chưa có test tự động)

**Handle:**
1. `search_products` → prepare bằng `ref` trả về → đặt hàng thành công.
2. Truyền handle bịa (`P99`) → nhận lỗi có `recover`, model tự gọi lại search, hội thoại không hỏng.
3. Sai loại: `prepare_order(productRef: "A1")` → lỗi, không nhận nhầm địa chỉ.
4. Search 2 lần cùng sản phẩm → cùng một handle (idempotent).
5. Hội thoại dài nhiều lượt → counter tăng liên tục, handle cũ vẫn resolve đúng.
6. Restart API giữa chừng → handle cũ mất → lỗi có hướng dẫn, không phải exception.
7. **Cross-scope (§2.5):** ở phòng hỗ trợ, gọi `get_chat_partner_info` rồi ép model truyền `W*` của
   khách vào `recommend_products` → phải nhận lỗi nói rõ "thuộc về khách hàng", không phải "không tìm thấy".
8. Đọc lại toàn bộ hội thoại: **không có GUID nào** trong tool result, thinking stream, và **phần văn
   bản model tự viết**. Block `@@payment:` là ngoại lệ duy nhất được phép có (§5).
9. Làm hỏng JSON trong block `@@payment:` → FE vẫn cắt block khỏi text, không hiện JSON thô ra chat.

**Draft:**
10. prepare → **reload trang** → "ok chốt" → đơn tạo đúng *(yêu cầu chính)*.
11. prepare → **restart API** → "ok chốt" → vẫn tạo được đơn.
12. prepare → chờ quá `DraftTtlMinutes` → confirm → báo hết hạn, không tạo đơn.
13. prepare 2 lần liên tiếp → bảng chỉ còn **một** dòng Pending (cái cũ đã bị XÓA, không phải đánh dấu).
14. **Race:** bắn **hai HTTP request `confirm_order` song song** (vòng lặp tool của AI chạy tuần tự
    nên không tự tạo được tình huống này — phải gọi thẳng API, hoặc đẩy 2 job trùng vào `AiBotQueue`)
    → đúng **1** đơn được tạo, request còn lại báo không có draft.
15. Đổi giá sản phẩm giữa prepare và confirm → báo giá mới, draft bị xóa, không tạo đơn.
16. Sau khi đặt thành công → truy vấn `ai_order_drafts` → **không còn dòng nào** của user đó.
17. Kill process ngay giữa `Confirming` → sau `ConfirmingGraceMinutes`, worker xóa dòng đó.
18. Tắt `AiOrderDraft:IsActive` → worker ngừng quét, không cần restart.
19. Soft-delete một draft thủ công (`UPDATE … SET is_deleted=true`) → `GetLatestPending` **không**
    trả về nó (kiểm `HasQueryFilter` đã khai đúng — §6.1 mục 3).

---

## 12. Tài liệu phải cập nhật sau khi merge

- `docs/api-documents/20-chat.md` — mô tả tool + tham số.
- `docs/api-documents/09-orders.md` — luồng đặt hàng qua AI.
- `docs/erd/SEP490_FengDeskAI.drawio` — thêm bảng `ai_order_drafts`.
- `ARCHITECTURE.md` §3.4 — thêm `AiOrderDraftCleanupWorker` vào danh sách background worker.
- `docs/adr/ai-order-tool-design.md` — ADR cũ mô tả draft trong cache: đánh dấu superseded bởi file này.
- `CLAUDE.md` mục "AI tools" — ghi rõ quy ước handle.
