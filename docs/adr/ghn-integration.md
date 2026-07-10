# GHN Integration Guide (FengDeskAI)

> How to integrate **GHN (Giao Hàng Nhanh)** as the shipping provider for FengDeskAI.
> Scope: **DB changes**, **project/code changes**, **webhook wired into orders**, and the **API calls to estimate
> shipping fee + create orders**. Multi-vendor aware (one order → many stores → many GHN shipments).
>
> Draft for review — you will edit/extend. Marked **TODO/CONFIRM** where a decision is yours.
> GHN API docs: https://api.ghn.vn/home/docs

---

## 1. Why GHN, and how it differs from AhaMove

- **Self-serve dev**: register at `5sao.ghn.dev` (staging) and get a **Token + ShopId immediately** — no business approval.
- **Address model is code-based, not GPS.** GHN routes by **`district_id` (int)** + **`ward_code` (string)**, not lat/lng.
  This is the main reason we need DB changes (§3).
- **Shop-based, fits multi-vendor.** One GHN account (one Token) can own **many shops**; each shop has a `ShopId`
  and its own pickup address. We map **one `GardenStore` → one GHN `ShopId`**. The `ShopId` is sent as an HTTP header
  per request, so each store's shipment is created from that store's pickup address automatically.
- **Nationwide** (not same-city only), so it works for cross-province orders too.

GHN flow:

```
1. (one-time) Create shop(s) + get Token & ShopId       (web portal 5sao.ghn.dev)
2. Sync GHN province/district/ward codes into our DB     (master-data APIs)
3. ESTIMATE fee per delivery                             (POST .../shipping-order/fee)
4. CREATE order per delivery                             (POST .../shipping-order/create)
5. GHN CALLBACKS our webhook on every status change      (we configure the URL with GHN)
6. CANCEL while not yet picked                           (POST .../switch-status/cancel)
```

---

## 2. Environments & credentials

| | Staging (dev) | Production |
|---|---|---|
| API base URL | `https://dev-online-gateway.ghn.vn` | `https://online-gateway.ghn.vn` |
| Portal (get Token/ShopId) | `https://5sao.ghn.dev` | `https://khachhang.ghn.vn` |
| Auth | header `Token: <token>` + header `ShopId: <shopId>` | same |

**Get credentials (staging):** log in to `5sao.ghn.dev` → tab **"Chủ cửa hàng"** → **"Xem"** to copy the **Token**;
tab **"Quản lý cửa hàng"** → create/select a shop and fill its address → that shop's **ShopId** is what you send.

GHN has **no Bearer/JWT and no token expiry flow like AhaMove** — the Token is a long-lived API key. Keep it secret.

**Config (empty defaults, real values from env — per `CLAUDE.md` §"Cấu hình & bảo mật"):**

```jsonc
// appsettings.json
"Ghn": {
  "BaseUrl": "https://dev-online-gateway.ghn.vn",
  "Token": "",                 // env: Ghn__Token
  "DefaultShopId": 0,          // env: Ghn__DefaultShopId (used when a store has no GhnShopId yet)
  "DefaultServiceTypeId": 2,   // 2 = e-commerce (light), 5 = traditional (heavy)
  "WebhookSecret": ""          // our own guard on the callback endpoint, see §6
}
```

---

## 3. Database changes

GHN needs codes we don't store yet. Three groups of changes.

### 3.1 Geography — store GHN codes (required)

Our `District.Code` / `Ward.Code` are `int` and are **not** guaranteed to equal GHN's codes. GHN `ward_code` is a
**string with possible leading zeros** (e.g. `"030712"`), so it cannot be an `int`. Add dedicated fields:

```csharp
// Domain/Entities/Geography/District.cs
public int? GhnDistrictId { get; set; }   // GHN DistrictID (int)

// Domain/Entities/Geography/Ward.cs
public string? GhnWardCode { get; set; }  // GHN WardCode (string, keep leading zeros)
```

> `Province` needs no code for fee/create (GHN identifies by district+ward). Keep as is.

EF config (Fluent API in `Infrastructure/Persistence/Configurations`, snake_case columns):

```csharp
builder.Property(d => d.GhnDistrictId).HasColumnName("ghn_district_id");
builder.Property(w => w.GhnWardCode).HasColumnName("ghn_ward_code").HasMaxLength(20);
```

These are populated by a **one-time sync** from GHN master-data APIs (§5.5), matched to our rows by name.

### 3.2 Vendor — map each store to a GHN shop (required for multi-vendor)

```csharp
// Domain/Entities/Vendor/GardenStore.cs
public int? GhnShopId { get; set; }          // this store's GHN ShopId (pickup origin)
public int GhnServiceTypeId { get; set; } = 2; // 2 e-commerce / 5 traditional  (CONFIRM default)
```

`StoreAddress` already has the needed sender fields (`SenderName`, `SenderPhone`, `StreetAddress`, `WardId`).
GHN takes pickup from the `ShopId` header, so once a store has a `GhnShopId` whose GHN address is set, we don't
even need to send `from_*`. (We can still send `from_*` from `StoreAddress` for clarity.)

> The existing `GardenStore.AhamoveServiceId` is now unused — you can drop it in the same migration or leave it.

### 3.3 Sales — keep our id on the GHN order (recommended)

No new columns needed: `Delivery` already has `ProviderOrderId`, `TrackingCode`, `ShippingProvider`, `TrackingUrl`,
`EstimatedDeliveryDate`. Mapping:

| GHN | `Delivery` field |
|---|---|
| `order_code` (create response) | `ProviderOrderId` **and** `TrackingCode` |
| we send `client_order_code = delivery.Id` | matches callbacks back to the delivery |
| `https://donhang.ghn.vn/?order_code=<order_code>` | `TrackingUrl` (CONFIRM public tracking URL format) |
| `expected_delivery_time` | `EstimatedDeliveryDate` |
| `"GHN"` | `ShippingProvider` |

### 3.4 Migration

```bash
dotnet ef migrations add GhnShippingFields -p src/FengDeskAI.Infrastructure -s src/FengDeskAI.WebAPI
dotnet ef database update                  -p src/FengDeskAI.Infrastructure -s src/FengDeskAI.WebAPI
```

Remember to update the ERD (`Documents/ERD/SEP490_FengDeskAI.drawio`) per `CLAUDE.md`.

---

## 4. Project / code changes

The shipping abstraction already exists — we add a GHN implementation and extend the request model.

### 4.1 Files involved

| Layer | File | Change |
|---|---|---|
| Application (port) | `Interfaces/External/IShippingProvider.cs` | add `EstimateFeeAsync`, `CancelShipmentAsync` (optional) |
| Application (models) | `Interfaces/External/ShippingProviderModels.cs` | extend `ShipmentRequest` (§4.2) |
| Infrastructure | `ExternalServices/Shipping/GhnShippingProvider.cs` | **new** — implements `IShippingProvider` |
| Infrastructure | `ExternalServices/Shipping/GhnSettings.cs` | **new** — config (§2) |
| Infrastructure | `DependencyInjection.cs` (line ~175) | register GHN instead of `MockShopee` |
| Application | `Features/Payment/Services/PaymentService.cs` (`CreateShipmentsAsync`) | pass pickup/dest GHN codes + items |
| WebAPI | `Controllers/ShippingController.cs` | add GHN webhook endpoint (§6) |
| Application | `Features/Shipping/Services/ShippingService.cs` | reuse `ProcessWebhookAsync` (no change to core) |

### 4.2 Extend `ShipmentRequest`

GHN needs both endpoints' district/ward codes, dimensions, weight, COD, and item list:

```csharp
// Application/Interfaces/External/ShippingProviderModels.cs
public record ShipmentRequest(
    Guid DeliveryId,
    Guid OrderId,
    decimal Subtotal,
    int ShopId,                 // GardenStore.GhnShopId  (pickup origin)
    int ServiceTypeId,          // 2 e-commerce / 5 traditional
    // destination (customer) — from UserAddress + Geography
    string RecipientName,
    string RecipientPhone,
    string ShippingAddress,
    int ToDistrictId,           // Ward.District.GhnDistrictId
    string ToWardCode,          // Ward.GhnWardCode
    // parcel
    int WeightGram,
    int LengthCm, int WidthCm, int HeightCm,
    decimal CodAmount,          // 0 when paid online via PayOS
    decimal InsuranceValue,     // = goods value, capped 5,000,000
    string RequiredNote,        // "KHONGCHOXEMHANG" | "CHOXEMHANGKHONGTHU" | "CHOTHUHANG"
    IReadOnlyList<ShipmentItem> Items);

public record ShipmentItem(string Name, string? Code, int Quantity, decimal Price,
                           int WeightGram, int LengthCm, int WidthCm, int HeightCm);

public record ShipmentResult(
    string Provider, string ProviderOrderId, string TrackingCode,
    DateTime? EstimatedDeliveryDate, string? TrackingUrl, decimal ShippingFee); // + fee
```

> Keep the provider **DB-free**: `PaymentService` reads `Store`, `UserAddress`, and the GHN codes from
> `Geography`, then passes them in. The provider only does HTTP. This respects Domain ← Application ← Infrastructure.

### 4.3 Shipping fee: API vs. our calculator

Today `ShippingFeeCalculator` computes a deterministic fee. With GHN you have two options:

- **A (recommended): use GHN's real fee.** Call `EstimateFeeAsync` (the `/fee` API, §5.3) per delivery and store
  the result in `Delivery.ShippingFee`. Most accurate; this is the courier's actual charge.
- **B: keep our calculator** for display and reconcile later. Simpler, but can drift from the real GHN fee.

Pick one source of truth so the customer's total stays consistent. (CONFIRM which.)

### 4.4 DI registration (swap the mock)

```csharp
// Infrastructure/DependencyInjection.cs
services.Configure<GhnSettings>(config.GetSection("Ghn"));
services.AddHttpClient<IShippingProvider, GhnShippingProvider>((sp, c) =>
{
    var cfg = sp.GetRequiredService<IOptions<GhnSettings>>().Value;
    c.BaseAddress = new Uri(cfg.BaseUrl);
    c.DefaultRequestHeaders.Add("Token", cfg.Token);   // ShopId added per-request (multi-vendor)
});
```

Tip: keep `MockShopeeShippingProvider` and choose via a config flag (`Shipping:Provider = "Ghn" | "Mock"`) so local
dev works without GHN credentials.

---

## 5. API reference (the calls we use)

All calls send headers `Token` and `ShopId` (except master-data, which needs only `Token`).
`Content-Type: application/json`.

### 5.1 Get available services — `POST /shiip/public-api/v2/shipping-order/available-services`

Returns the `service_id` / `service_type_id` valid for a route. Use it to pick `service_type_id` (2 = e-commerce,
5 = traditional). Body: `{ "shop_id": <int>, "from_district": <int>, "to_district": <int> }`.

### 5.2 Master data (one-time / cached) — to fill GHN codes (§3.1)

- Provinces: `GET /shiip/public-api/master-data/province`
- Districts: `POST /shiip/public-api/master-data/district`  body `{ "province_id": <int> }`
- Wards: `POST /shiip/public-api/master-data/ward`  body `{ "district_id": <int> }`

Run once (or scheduled) to map our `Province/District/Ward` rows → `GhnDistrictId` / `GhnWardCode` by name.

### 5.3 Estimate fee — `POST /shiip/public-api/v2/shipping-order/fee`  ⭐ (price estimate)

```bash
curl -X POST 'https://dev-online-gateway.ghn.vn/shiip/public-api/v2/shipping-order/fee' \
  -H 'Content-Type: application/json' \
  -H 'Token: <token>' -H 'ShopId: <shopId>' \
  -d '{
    "service_type_id": 2,
    "from_district_id": 1442, "from_ward_code": "21211",
    "to_district_id": 1820,  "to_ward_code": "030712",
    "weight": 3000, "length": 30, "width": 40, "height": 20,
    "insurance_value": 250000,
    "items": [ { "name": "Cây kim tiền", "quantity": 1,
                 "weight": 1000, "length": 20, "width": 20, "height": 20 } ]
  }'
```

```jsonc
// 200
{ "code": 200, "message": "Success",
  "data": { "total": 36300, "service_fee": 36300, "insurance_fee": 0, "cod_fee": 0, ... } }
```

Use `data.total` as the shipping fee (VND). Notes:
- `service_type_id` **2 = light** (use top-level `weight`/dims), **5 = heavy** (dims come from `items[]`).
- `weight` in **grams** (max 1,600,000). Dimensions in **cm** (max 200 each).
- If `from_*` omitted, GHN uses the shop (`ShopId`) address — convenient for multi-vendor.
- Chargeable weight = max(actual, volumetric `L×W×H/5`).

### 5.4 Create order — `POST /shiip/public-api/v2/shipping-order/create`

```bash
curl -X POST 'https://dev-online-gateway.ghn.vn/shiip/public-api/v2/shipping-order/create' \
  -H 'Content-Type: application/json' \
  -H 'Token: <token>' -H 'ShopId: <shopId>' \
  -d '{
    "payment_type_id": 1,                 // 1 = shop pays ship, 2 = buyer pays
    "required_note": "KHONGCHOXEMHANG",
    "client_order_code": "<delivery.Id>", // OUR id -> comes back in callbacks (idempotent)
    "to_name": "Nguyễn Văn A", "to_phone": "0987654321",
    "to_address": "72 Thành Thái, P14, Q10, HCM",
    "to_ward_code": "20308", "to_district_id": 1444,
    "cod_amount": 0,                      // 0 when already paid via PayOS
    "weight": 2000, "length": 20, "width": 20, "height": 20,
    "insurance_value": 450000,
    "service_type_id": 2,
    "items": [ { "name": "Cây kim tiền", "code": "P1", "quantity": 1, "price": 450000,
                 "weight": 2000, "length": 20, "width": 20, "height": 20 } ]
  }'
```

```jsonc
// 200
{ "code": 200, "message": "Success",
  "data": { "order_code": "FFFNL9HH", "total_fee": "33000",
            "expected_delivery_time": "2026-06-29T16:00:00Z", "fee": { ... } } }
```

Map `order_code → ProviderOrderId/TrackingCode`, `total_fee → ShippingFee`,
`expected_delivery_time → EstimatedDeliveryDate`. `from_*` optional (taken from `ShopId`).

- `required_note` (**required**): `KHONGCHOXEMHANG` (no view), `CHOXEMHANGKHONGTHU` (view, no trial), `CHOTHUHANG` (trial).
- `payment_type_id`: **1** shop pays shipping (we collected online), **2** buyer pays.
- `client_order_code` is **unique** — re-sending the same value returns the existing order (use `delivery.Id` for idempotency).

### 5.5 Cancel order — `POST /shiip/public-api/v2/switch-status/cancel`

```bash
curl -X POST 'https://dev-online-gateway.ghn.vn/shiip/public-api/v2/switch-status/cancel' \
  -H 'Token: <token>' -H 'ShopId: <shopId>' -H 'Content-Type: application/json' \
  -d '{ "order_codes": ["FFFNL9HH"] }'
```

Only possible before the parcel is picked (`ready_to_pick` / `picking`). After that, use the Returns flow.

### 5.6 Common pitfalls

| Symptom | Cause / fix |
|---|---|
| `400 USER_ERR_COMMON` | bad body / wrong `required_note` value / missing `items` for heavy service. |
| Wrong/blank fee | `from_*` or `to_*` district/ward codes not GHN codes → fix master-data sync (§3.1). |
| Address rejected | `ward_code` lost leading zeros (stored as int) → must be **string** (§3.1). |
| Duplicate order | same `client_order_code` returns the existing order — that's intended idempotency. |

---

## 6. Webhook wired into orders

GHN POSTs JSON to a single URL you register with them (give GHN: **Client ID**, **webhook URL**, **env**, **name**).
You must reply **HTTP 200**; otherwise GHN retries **10×, 5s apart**.

> ⚠️ GHN's callback has **no signature/secret**. Protect the endpoint yourself — e.g. a secret path segment or a
> query token only you and GHN know (`/api/shipping/ghn/webhook?key=<secret>`), validated against `Ghn:WebhookSecret`.

### 6.1 Callback shape

```jsonc
{
  "Type": "switch_status",          // create | switch_status | update_weight | update_cod | update_fee
  "OrderCode": "Z82BS",             // GHN order  -> matches Delivery.ProviderOrderId
  "ClientOrderCode": "<delivery.Id>",// OUR id    -> matches Delivery.Id
  "Status": "delivering",           // see §7
  "ShopID": 81558,
  "TotalFee": 71400,
  "Fee": { "MainService": 53900, "Insurance": 17500, "CODFee": 0, ... },
  "Weight": 200, "CODAmount": 0,
  "Time": "2026-06-26T03:52:50Z"
}
```

### 6.2 Adapter → existing `ProcessWebhookAsync`

Add a GHN endpoint that translates the payload into our normalized `ShippingWebhookRequest` and calls the **existing**
`IShippingService.ProcessWebhookAsync` — the core (raw-save → resolve delivery → validate transition → progress log →
order roll-up → customer notification) is reused unchanged.

```csharp
// WebAPI/Controllers/ShippingController.cs  (new action)
[HttpPost("ghn/webhook")]
[AllowAnonymous]
public async Task<IActionResult> GhnWebhook([FromBody] GhnCallback cb,
    [FromQuery] string key, CancellationToken ct)
{
    if (string.IsNullOrEmpty(_ghn.WebhookSecret) || key != _ghn.WebhookSecret)
        return Unauthorized();

    // Only status changes move our state machine; other Types are logged/handled separately.
    if (cb.Type is "create" or "switch_status")
    {
        var req = new ShippingWebhookRequest
        {
            Provider        = "GHN",
            EventType       = cb.Type,
            ProviderOrderId = cb.OrderCode,           // ResolveDelivery matches (Provider + ProviderOrderId)
            // or: DeliveryId = Guid.Parse(cb.ClientOrderCode)
            NewStatus       = MapGhnStatus(cb.Status), // §7
            TrackingCode    = cb.OrderCode,
            RawPayload      = JsonSerializer.Serialize(cb)
        };
        return ToActionResult(await _service.ProcessWebhookAsync(req, ct));
    }

    // update_fee / update_weight / update_cod → update Delivery.ShippingFee/weight + log (optional)
    return Ok();
}
```

`ResolveDeliveryAsync` in `ShippingService` already matches by `(Provider + ProviderOrderId)`. The roll-up to the
parent `Order` (`OrderWorkflow.ComputeOrderStatus`) and the customer `Notification` are produced automatically.
The flow is **idempotent** (raw webhook saved first; invalid transitions are logged and skipped), so GHN retries are safe.

---

## 7. Status mapping (GHN → our `DeliveryStatus`)

GHN status list → `Domain.Enums.Sales.DeliveryStatus`:

| GHN `Status` | Our `DeliveryStatus` | Notes |
|---|---|---|
| `ready_to_pick`, `picking`, `money_collect_picking` | `Confirmed` | order created / awaiting pickup |
| `picked`, `storing`, `transporting`, `sorting`, `delivering`, `money_collect_delivering` | `Shipped` | in transit; set `ShippedAt` on first |
| `delivered` | `Delivered` | set `DeliveredAt` |
| `delivery_fail` | `DeliveryFailed` | |
| `waiting_to_return`, `return`, `return_transporting`, `return_sorting`, `returning`, `return_fail`, `returned` | `Returned` | returning to store |
| `cancel` | `Cancelled` | |
| `exception`, `damage`, `lost` | `DeliveryFailed` *(CONFIRM)* | flag for manual handling |

> Make sure `OrderWorkflow.IsValidDeliveryTransition` allows these transitions (e.g. `Confirmed → Shipped → Delivered`,
> and `→ DeliveryFailed/Cancelled/Returned`). Adjust the workflow if GHN sends a transition it currently rejects.

---

## 8. End-to-end multi-vendor flow

```
Customer pays order #1234 (Store A items + Store B items), paid online via PayOS
        │  PaymentService (on Paid) → SplitDeliveries
        ▼
 Delivery_A (Store A)                         Delivery_B (Store B)
        │ CreateShipmentsAsync loop (one GHN order per delivery)
        ▼                                            ▼
 POST /shipping-order/create                  POST /shipping-order/create
 header ShopId = A.GhnShopId                  header ShopId = B.GhnShopId
 client_order_code = Delivery_A.Id            client_order_code = Delivery_B.Id
        │ order_code → ProviderOrderId                │
        ▼                                            ▼
   GHN callbacks  ───►  POST /api/shipping/ghn/webhook?key=...  ───►  ProcessWebhookAsync
        │                                                                  │
        ▼                                                                  ▼
 Delivery_A.Status + progress log + customer notification    OrderWorkflow rolls Order #1234 up
```

Rules:
- **One `Delivery` ⇒ one GHN order** (one shop pickup). Never merge stores.
- **Per-store `ShopId`** selects the pickup address — the heart of multi-vendor with GHN.
- **Fee is per delivery** (`/fee` per delivery, store on `Delivery.ShippingFee`).
- Parent `Order` completes only when all deliveries reach a terminal state (already handled).
- Each active `GardenStore` needs: a GHN `ShopId` with its address set on the portal, and `GhnServiceTypeId`.
- Each `Ward`/`District` used must have `GhnWardCode` / `GhnDistrictId` populated (master-data sync).

---

## 9. Re-delivery (giao lại hàng)

"Giao lại" splits into two cases. **One is already working; the other is an optional add-on.**

### 9.1 Passive re-delivery — GHN re-attempts automatically (already supported)

When a delivery attempt fails, GHN does **not** return the parcel immediately — it retries delivery on its own
(up to 3 attempts, status `waiting_to_return` means "still deliverable within 24/48h"). We only receive webhooks.

Our state machine already accepts this round-trip — `OrderWorkflow.IsValidDeliveryTransition` allows:

```
Shipped → DeliveryFailed → Shipped → Delivered
```

So the webhook flow needs **no extra code**:

```
GHN delivery attempt fails   → webhook Status "delivery_fail"  → DeliveryFailed
GHN re-attempts (auto)       → webhook Status "delivering"     → Shipped   (transition allowed)
GHN delivers successfully    → webhook Status "delivered"      → Delivered
```

`GhnStatusMapper` already maps `delivery_fail → DeliveryFailed` and `delivering → Shipped`, and each transition
produces a progress log + customer notification via `ProcessWebhookAsync`. **Nothing to build here.**

> The customer just sees the status bounce `DeliveryFailed → Shipped → Delivered` across notifications. If you want
> to surface "attempt #2 of 3", read GHN's failure reason from the callback (`Reason` / `ReasonCode`) and store it
> on the progress log `Note`.

### 9.2 Active re-delivery — we ask GHN to try again (✅ implemented)

> **Status: done.** `IShippingProvider.RedeliverAsync(orderCode, shopId)` → `GhnShippingProvider` POSTs to
> `/shiip/public-api/v2/switch-status/storing` (⚠️ confirm exact path vs GHN doc id=65). Exposed as
> `POST /api/shipping/deliveries/{deliveryId}/redeliver` (owner/staff of the store or admin). Allowed only from
> `DeliveryFailed`; we don't flip status — the next `delivering` webhook moves `DeliveryFailed → Shipped`.
> Mock/AhaMove return `false` (not supported) via the interface default.

Original plan (for reference):

Use this only if a **store/staff wants to manually trigger another delivery attempt** (e.g. after the parcel reached
`waiting_to_return` and the customer rescheduled). GHN exposes a **"Delivery Again"** API for this
([doc id=65](https://api.ghn.vn/home/docs/detail?id=65)).

What to add:

```csharp
// 1) Application port — optional method on IShippingProvider
Task<bool> RedeliverAsync(string providerOrderCode, CancellationToken ct = default);

// 2) Infrastructure — GhnShippingProvider
//    POST {BaseUrl}/shiip/public-api/v2/switch-status/storing   // CONFIRM exact path from doc id=65
//    headers Token + ShopId, body { "order_codes": ["<order_code>"] }

// 3) WebAPI — ShippingController, owner/staff of the store only
[HttpPost("deliveries/{deliveryId:guid}/redeliver")]
public Task<IActionResult> Redeliver(Guid deliveryId, CancellationToken ct) => /* call provider, log */;
```

Notes / decisions (**CONFIRM**):

- Allowed only while the parcel is still in GHN's hands (not yet `returned`). Validate current `Delivery.Status`
  before calling — typically only from `DeliveryFailed` / `waiting_to_return`.
- After GHN accepts, **don't flip our status manually** — let the next `delivering` webhook move `DeliveryFailed → Shipped`,
  keeping the webhook as the single source of truth.
- Verify the exact endpoint and body for "Delivery Again" against GHN doc id=65 (path not pinned here).

> **Different from Returns.** The `Returns` context (`ReturnService` / `RefundService`) is customer-initiated
> return + refund of delivered goods. Re-delivery is re-attempting a **failed** delivery — unrelated flows.

---

## 10. Address data & GHN code sync

Goal: go from "a few test wards in the DB" to **full VN administrative data**, each row also carrying its **GHN code**.
Two independent steps — run them in order.

```
Step A: import government (GSO) province/district/ward  → fills Province/District/Ward (+ gov Code)
Step B: call GHN master-data + match                    → fills GhnProvinceId / GhnDistrictId / GhnWardCode
```

### 10.1 Step A — import government administrative data

Today the tables only hold test regions. Import the full tree so every store/customer address can be expressed.

- **Source (pick one, CONFIRM):**
  - Open dataset `https://provinces.open-api.vn/api/?depth=3` — one call returns the full province→district→ward tree
    with **GSO codes**. Easiest for a one-off seed.
  - Or a committed JSON snapshot under `Documents/` (no network at seed time, reproducible).
- **Upsert by government `Code`** (idempotent): match existing rows by `Code`; insert the rest. Never hard-delete
  (respect `IsDeleted`) — existing `UserAddress`/`StoreAddress` FKs point at `Ward.Id`.

```
for each province in dataset:
    upsert Province by Code            (Name, Code)
    for each district:
        upsert District by Code        (ProvinceId, Name, Code)
        for each ward:
            upsert Ward by Code         (DistrictId, Name, Code)   // gov ward code as int
```

> Keep our own `Guid Id` stable. We only add/refresh rows; we don't renumber.

### 10.2 Step B — sync GHN codes onto those rows

Call GHN master-data and fill the `Ghn*` columns (from §3.1). Match **top-down** so repeated ward names don't collide.

GHN endpoints (header `Token` only):

| | Endpoint | Body | Key fields returned |
|---|---|---|---|
| Provinces | `GET /shiip/public-api/master-data/province` | — | `ProvinceID`, `ProvinceName`, `Code`, `NameExtension[]` |
| Districts | `POST /shiip/public-api/master-data/district` | `{ "province_id": <int> }` | `DistrictID`, `Code` (gov code, e.g. `"0201"`), `DistrictName`, `NameExtension[]` |
| Wards | `POST /shiip/public-api/master-data/ward` | `{ "district_id": <int> }` | `WardCode` (GHN, string), `WardName`, `NameExtension[]` |

**Matching rules (reliable → fuzzy):**

```
Province:  our Province.Code  ==  GHN Code            → GhnProvinceId = GHN ProvinceID
District:  within matched province:
           our District.Code  ==  GHN Code (padded)   → GhnDistrictId = GHN DistrictID
           fallback: normalized name ∈ {DistrictName} ∪ NameExtension
Ward:      within matched district (GHN has NO gov code for wards):
           normalized name ∈ {WardName} ∪ NameExtension  → GhnWardCode = GHN WardCode
```

- **District/Province match by gov `Code`** — GHN returns it, so this is exact, no name guessing.
- **Ward must match by name** (+ `NameExtension` aliases) **inside the already-matched district** — that's why
  the hierarchy matters (`Phường 1` exists in many districts).
- Watch the `Code` format: GHN district `Code` is a **zero-padded string** (`"0201"`); compare as string.

**Normalize names before comparing** (both sides): strip Vietnamese diacritics, lowercase, collapse spaces, drop
prefixes (`Tỉnh/Thành phố/TP/Thị xã/Quận/Huyện/Phường/Xã/Thị trấn`). Leftovers → fuzzy (Levenshtein ≥ ~0.9).

**Idempotent + reportable:** only fill rows where `Ghn* IS NULL`; log every unmatched row (esp. wards) for manual
mapping, and keep a small override dictionary for known mismatches.

### 10.3 Where it lives & how to run

- New service in `Application/Features/Geography/Services/` (e.g. `IGeoSyncService`): `ImportGovernmentDataAsync()`
  + `SyncGhnCodesAsync()`. GHN HTTP calls reuse the GHN typed client from Infrastructure.
- Expose as a **seed command** (like the existing `seed`), not a runtime endpoint — it's a one-off / occasional job:

```bash
dotnet run --project src/FengDeskAI.WebAPI -- sync-geo      # Step A then Step B
```

### 10.4 No-duplicates & correctness verification

**A. Avoid duplicate rows (idempotent import).**

Never rely on the `Guid Id` to prevent duplicates — use a **natural key** = administrative code scoped by parent:

| Table | Natural (unique) key |
|---|---|
| `Province` | `Code` |
| `District` | `(ProvinceId, Code)` |
| `Ward` | `(DistrictId, Code)` |

- Add a **UNIQUE index** for each (EF Fluent: `builder.HasIndex(d => new { d.ProvinceId, d.Code }).IsUnique();`).
  This is the **last-line safety net** — even a logic bug or concurrent run can't insert a duplicate.
- **Upsert by the natural key** (look up by `Code` within parent → update if found, else insert). Re-running
  `sync-geo` then always converges to the same rows:

```csharp
var d = await db.Districts
    .FirstOrDefaultAsync(x => x.ProvinceId == province.Id && x.Code == code, ct);
if (d is null) db.Districts.Add(new District { ProvinceId = province.Id, Code = code, Name = name });
else           d.Name = name;   // refresh only
```

- **Don't match by name** for de-dup — names repeat (`Phường 1`), change spelling, and vary by diacritics. Code is stable.

> ⚠️ **Existing test rows with wrong/made-up `Code`** will be *missed* by upsert-by-code → it inserts a name-duplicate.
> Fix once before enabling the unique index: match the test rows to the GSO dataset **by name**, correct their `Code`,
> then turn on `UNIQUE` + upsert-by-code. Rows already referenced by `UserAddress`/`StoreAddress` (FK on `Ward.Id`)
> must be *corrected in place*, not deleted.

**B. Verify the codes are correct (cheap → strongest).**

1. **Tree integrity:** every `District.ProvinceId` and `Ward.DistrictId` resolves to a real parent — no orphans.
2. **Count check vs GSO source:** each province has the expected number of districts/wards as in the dataset.
3. **Match-rate against GHN (the free correctness signal):** GHN returns the **government `Code`** for provinces &
   districts, so during Step B a `Code` match *proves* the code is right. **Any unmatched district = a suspect code** —
   list them. (Wards have no gov code on GHN's side, so ward nulls are usually name-mismatch, not wrong code.)
4. **End-to-end smoke test (ground truth):** call `/fee` (§5.3) with a sample `district_id + ward_code` derived from
   the DB. A `200` + sane `total` proves the codes work end-to-end; an error/wrong area means the code is wrong.

> After `sync-geo`, print a **report**: count `GhnDistrictId IS NULL` and `GhnWardCode IS NULL`, and list the rows.
> Many district nulls ⇒ code problem; ward nulls ⇒ expected name drift (map by hand / override dictionary).

### 10.5 Caveats

- **2025 administrative reform:** VN merged communes / restructured units in 2025. If the GSO dataset and GHN's data
  are on different versions, some wards won't match — verify a few real samples before bulk-running, and rely on the
  unmatched report.
- Only sync the regions you actually serve to keep it fast (e.g. start with the cities where stores operate).

---

## 11. Implementation checklist

**Code (done — wired into the `Shipping` context):**

- [x] DB: add `District.GhnDistrictId`, `Ward.GhnWardCode`, `GardenStore.GhnShopId` + `GhnServiceTypeId` (+ EF configs); migration `GhnShippingFields`.
- [x] Extend shared `ShipmentRequest` (GHN codes + dims) and `ShipmentItem` (per-item weight/dims); fields are optional so AhaMove/Mock are unaffected (§4.2).
- [x] `ShipmentRequestBuilder` fills GHN codes from store/customer ward chain + aggregates parcel dims.
- [x] Implement `GhnShippingProvider : IShippingProvider` (typed HttpClient, `Token` default header, `ShopId` per request) — create order.
- [x] Pass GHN data + items at all 3 creation points (`PaymentService`, `OrderService` COD, `ReturnService` exchange) via the shared builder; store `order_code` → `ProviderOrderId`/`TrackingCode`, ETA, tracking URL.
- [x] Add `POST /api/shipping/ghn/webhook?key=<ShippingWebhook:Secret>` → `ProcessWebhookAsync`; `GhnStatusMapper.Map` per §7; resolve by `ClientOrderCode` (delivery.Id) or `OrderCode`.
- [x] `OrderWorkflow.IsValidDeliveryTransition` extended for courier flow (Confirmed→Shipped, →DeliveryFailed, →Returned).
- [x] DI: select provider by `Shipping:Provider = "Ghn" | "Ahamove" | "Mock"` — **default now `Ghn`**.
- [x] **Fee (option A):** `EstimateFeeAsync` on `IShippingProvider`; `GhnShippingProvider` calls `/fee`. `DeliveryFeeEstimator` computes per-store fee **at checkout** (`OrderService`) and adds it to `order.TotalShippingFee`/`TotalAmount` (COD + online). On any GHN error / missing codes it **falls back to `ShippingFeeCalculator`** so checkout never breaks. Actual `total_fee` from the create response is stored back on `Delivery.ShippingFee` (online).
- [x] **Re-delivery (§9):** passive (GHN auto-retry) already covered by the transition table; active re-delivery endpoint `POST /api/shipping/deliveries/{id}/redeliver` implemented.
- [x] **Geo sync (§10):** `IGeoSyncService` / `GeoSyncService` — Step A imports the full VN tree from `provinces.open-api.vn` (upsert by gov `Code`), Step B fills `GhnProvinceId`/`GhnDistrictId`/`GhnWardCode` from GHN master-data (Code → name fallback; wards by name within matched district). Province gets `GhnProvinceId` (migration `GhnProvinceCode`). Run: `dotnet run --project src/FengDeskAI.WebAPI -- sync-geo`.

**Ops / business (still to do — needs real credentials & data):**

- [ ] Register on `5sao.ghn.dev`, create shop(s), copy **Token** + **ShopId**; set env (`Ghn__Token`, `Ghn__DefaultShopId`, `Shipping__Provider=Ghn`).
- [ ] Run `sync-geo` to fill `GhnDistrictId`/`GhnWardCode` (needs `Ghn__Token`); review the unmatched-rows report and add manual overrides for any leftover wards.
- [ ] Set each active store's `GhnShopId` (+ `GhnServiceTypeId`) and a valid `SenderPhone`.
- [ ] Configure `ShippingWebhook:Secret`; register the webhook URL (`…/api/shipping/ghn/webhook?key=<secret>`) + Client ID + env with GHN.
- [ ] Apply migrations: `dotnet ef database update -p src/FengDeskAI.Infrastructure -s src/FengDeskAI.WebAPI` (`GhnShippingFields`, `GhnProvinceCode`).
- [ ] Update ERD (`Documents/ERD/SEP490_FengDeskAI.drawio`).

---

## Sources

- [GHN — Create Account, Get Token, ShopID](https://api.ghn.vn/home/docs/detail?id=49)
- [GHN — Calculate Fee (estimate)](https://api.ghn.vn/home/docs/detail?id=95)
- [GHN — Create Order](https://api.ghn.vn/home/docs/detail?id=123)
- [GHN — Callback order status (webhook)](https://api.ghn.vn/home/docs/detail?id=47)
- [GHN — List of shipping status](https://api.ghn.vn/home/docs/detail?id=48)
- [GHN — Cancel / Get District / Get Ward / Get Service](https://api.ghn.vn/home/docs)
