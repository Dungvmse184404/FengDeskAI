# AhaMove Integration Guide (FengDeskAI)

> How to plug **AhaMove on-demand delivery** into the FengDeskAI backend.
> Covers: the AhaMove API itself, how to wire it into our Clean Architecture (`Shipping` context),
> the **multi-vendor** flow (one order → many stores → many shipments), and webhook/status handling.
>
> API version referenced: **AhaMove v3** (`/v3/...`). Docs: https://developers.ahamove.com/en/docs/introduction

---

## 1. The big picture

AhaMove is a Vietnamese same-city courier (BIKE / ECO / TRUCK …). It works like this:

```
1. Get a TOKEN for the order-creator account      (Account API)
2. (optional) ESTIMATE the fee before you commit   (POST /v3/orders/estimates)
3. CREATE an order: pickup point → drop-off point   (POST /v3/orders)
4. AhaMove finds a driver, then CALLBACKS your webhook on every status change
5. You can CANCEL while the order is still early    (DELETE /v3/orders/{id})
```

Two things make our case specific:

- **We are multi-vendor.** A customer order can contain items from several `GardenStore`s. Each store ships
  from its **own address**, so each store becomes **one separate AhaMove order** (one pickup → one drop-off).
  In our domain this is already modelled: one `Delivery` row = one store's portion of an `Order`.
- AhaMove is **same-city** (intra-province). It is the right courier when pickup and drop-off are in the same
  city. For cross-province you'd use a different provider — see §8.

---

## 2. Environments & credentials

|          | Staging (UAT)                              | Production                                                                    |
| -------- | ------------------------------------------ | ----------------------------------------------------------------------------- |
| Base URL | `https://partner-apistg.ahamove.com`       | `https://partner-api.ahamove.com` *(confirm exact host in your golive email)* |
| API key  | `API_KEY_STG` (emailed after registration) | `API_KEY` (emailed after you pass UAT)                                        |
|          |                                            |                                                                               |

Onboarding steps (from AhaMove "Overall Process"):

1. Register on the [Integration Form](https://forms.gle/BRAa8Axz7MqdJPdr6) → receive a **staging API key** by email.
2. Integrate on staging, complete the recommended test cases, submit the [UAT Completion Form](https://forms.gle/HAo3hVUw6XEq4M1a8).
3. Once approved → receive **production API key** → golive.

**Config (never commit secrets — follow `CLAUDE.md` §"Cấu hình & bảo mật"):**

```jsonc
// appsettings.json — empty defaults only
"Ahamove": {
  "BaseUrl": "https://partner-apistg.ahamove.com",
  "ApiKey": "",            // from env: Ahamove__ApiKey
  "Mobile": "",            // the partner account phone used to create orders
  "WebhookSecret": ""      // reuse existing ShippingWebhook secret, see §6
}
```

Real values come from env vars (`Ahamove__ApiKey`, `Ahamove__Mobile`, …) or local `.env`.

---

## 3. AhaMove API reference (only the endpoints we need)

All order endpoints require header `Authorization: Bearer <token>` and `Content-Type: application/json`.

### 3.1 Get a token — `POST /v3/accounts/token`

The token is per **account** (a phone number registered under our partner API key). It expires, so cache it
and refresh on `401 NOT_AUTHORIZED`.

```bash
curl -X POST 'https://partner-apistg.ahamove.com/v3/accounts/token' \
  -H 'Content-Type: application/json' \
  -d '{ "mobile": "849xxxxxxxx", "api_key": "<API_KEY_STG>" }'
```

```jsonc
// 200 OK
{ "token": "eyJ0eXAi...", "refresh_token": "0c42af21..." }
```

> First time only: register the account with `POST /v3/accounts` (same body + `name` + `address`).
> After that, `POST /v3/accounts/token` is enough to get a fresh token.
> **When a new token is issued, the old one is invalidated** — keep a single cached token per account.

### 3.2 Estimate fee — `POST /v3/orders/estimates`

Use this to show the shipping fee **before** creating the order (or to validate that the service covers the route).
You can ask for several services at once.

Request (key parts):

```jsonc
{
  "order_time": 0,                       // 0 = ship now
  "path": [
    { "lat": 10.7697, "lng": 106.6636,
      "address": "7/28 Thành Thái, Phường 14, Quận 10, Thành phố Hồ Chí Minh",
      "name": "Store A", "mobile": "0909..." },          // [0] = PICKUP (store)
    { "lat": 10.8018, "lng": 106.7144,
      "address": "475A Điện Biên Phủ, Phường 25, Bình Thạnh, Thành phố Hồ Chí Minh",
      "name": "Customer", "mobile": "0912...",
      "cod": 0, "item_value": 250000, "tracking_number": "<delivery.Id>" }  // [1] = DROP-OFF
  ],
  "services": [ { "_id": "SGN-BIKE", "requests": [] } ],
  "payment_method": "BALANCE"            // see §3.4
}
```

Response — array, one entry per service:

```jsonc
[ { "service_id": "SGN-BIKE",
    "data": { "distance": 11.98, "total_fee": 86000, "total_price": 86000, ... } } ]
```

Read `data.total_price` (or `total_fee`) as the shipping fee in VND.

### 3.3 Create order — `POST /v3/orders`

```jsonc
{
  "order_time": 0,
  "path": [
    { "lat": 10.7697, "lng": 106.6636, "address": "<store address>",
      "name": "<store name>", "mobile": "<store sender phone>",
      "remarks": "Mã đơn FengDesk: <order code>" },                 // PICKUP
    { "lat": 10.8018, "lng": 106.7144, "address": "<customer address>",
      "name": "<customer name>", "mobile": "<customer phone>",
      "cod": 0,                       // 0 because we collect online via PayOS (see §3.4)
      "item_value": 250000,           // for insurance / liability
      "tracking_number": "<delivery.Id>",   // <-- OUR delivery id; comes back in callbacks
      "remarks": "Giao hàng cẩn thận" }                             // DROP-OFF
  ],
  "service_id": "SGN-BIKE",
  "requests": [],                     // special add-ons: TIP, BULKY, FRAGILE...
  "payment_method": "BALANCE",
  "items": [ { "_id": "P1", "name": "Cây kim tiền mini", "price": 450000, "num": 1 } ],
  "package_detail": [ { "weight": 2.0, "description": "Đồ phong thủy" } ]
}
```

Response:

```jsonc
{
  "order_id": "24ABCD",                                  // AhaMove order id  -> ProviderOrderId
  "status": "ASSIGNING",
  "shared_link": "https://express.ahamove.com/s/24...",  // public tracking  -> TrackingUrl
  "order": { "total_price": 33000, ... }
}
```

**Field-to-domain mapping** when we create a shipment:

| AhaMove response | FengDeskAI `Delivery` field |
|---|---|
| `order_id` | `ProviderOrderId` |
| `shared_link` | `TrackingUrl` |
| `service_id` (e.g. `SGN-BIKE`) | `ShippingProvider` = `"Ahamove"` |
| `tracking_number` we sent (`delivery.Id`) | used to match callbacks back to the delivery |

### 3.4 `service_id`, `requests`, `payment_method`

- **`service_id = <city> + "-" + <group>`**, e.g. `SGN-BIKE`, `HAN-BIKE`, `SGN-ECO`, `SGN-TRUCK-500`.
  This is why `GardenStore.AhamoveServiceId` exists (e.g. `"SGN-BIKE"`) — it's the default service for that store's city.
  Alternatively send `group_service_id` (e.g. `"BIKE"`) and let AhaMove pick the city from the pickup address.
- **`requests`** = paid add-ons. `_id = service_id + "-" + GROUP`, e.g. `SGN-BIKE-FRAGILE`, `SGN-BIKE-BULKY` (with `tier_code`).
- **`payment_method`**:
  - `BALANCE` — deducted from our partner AhaMove wallet (recommended: we already collected money via PayOS).
  - `CASH` — sender pays the driver cash.
  - `CASH_BY_RECIPIENT` — recipient pays cash (use only for our COD orders).

  Rule of thumb for us: **online-paid order → `BALANCE`, COD order → `CASH_BY_RECIPIENT` with `cod` set** at the drop-off path.

### 3.5 Cancel order — `DELETE /v3/orders/{order_id}`

Only cancellable while status is `IDLE`, `ASSIGNING`, `ACCEPTED`, `CONFIRMING`, or `PAYING`. Once `IN PROCESS`, you can't.

```bash
curl -X DELETE 'https://partner-apistg.ahamove.com/v3/orders/24ABCD' \
  -H 'Authorization: Bearer <token>' -H 'Content-Type: application/json' \
  -d '{ "comment": "Khách hàng muốn hủy đơn" }'
```

You can also cancel by our tracking number: `DELETE /v3/orders/tracks` with `{ "tracking_number": "<delivery.Id>", "comment": "..." }`.

### 3.6 Common error codes

| HTTP | Code | Meaning / what to do |
|---|---|---|
| 401 | `NOT_AUTHORIZED` | Token expired → refresh token and retry once. |
| 404 | `SERVICE_NOT_FOUND` | Wrong `service_id` for that city. |
| 406 | `SERVICE_NOT_VALID_AT_PICKUP` / `INVALID_PICKUP_AREA` | AhaMove doesn't serve the store's area → fall back to another provider. |
| 406 | `INVALID_MAX_DISTANCE` | Route too far for the service (it's same-city) → fall back. |
| 406 | `NOT_ENOUGH_CREDIT` | Partner wallet empty (when `payment_method=BALANCE`). |
| 409 | `DUPLICATE_TRACKING_NUMBER` | We re-sent the same `delivery.Id` → make creation idempotent (§5.3). |

---

## 4. Order status flow (AhaMove → our `DeliveryStatus`)

AhaMove statuses: `IDLE → ASSIGNING → ACCEPTED → IN PROCESS → COMPLETED` (or `CANCELLED`).
The final delivery result is on the **drop-off path** (`path[i].status` for `i>0`): `COMPLETED` or `FAILED`,
plus order `sub_status` `IN_RETURN` / `RETURNED`.

Map them onto our existing `Domain.Enums.Sales.DeliveryStatus`:

| AhaMove `status` (+ context) | Our `DeliveryStatus` | Notes |
|---|---|---|
| `ASSIGNING` / `IDLE` | `Confirmed` | order created, looking for driver |
| `ACCEPTED` | `Confirmed` | driver assigned (optionally keep `Confirmed`) |
| `IN PROCESS` | `Shipped` | driver picked up the package; set `ShippedAt` |
| `COMPLETED` + drop-off `path.status = COMPLETED` | `Delivered` | set `DeliveredAt` |
| `COMPLETED` + drop-off `path.status = FAILED` | `DeliveryFailed` | |
| `sub_status = RETURNED` | `Returned` | package returned to store |
| `CANCELLED` | `Cancelled` | check `cancel_by_user` for reason text |

> Our `ShippingService` already validates transitions via `OrderWorkflow.IsValidDeliveryTransition` and rolls the
> parent `Order` status up from all its deliveries (`OrderWorkflow.ComputeOrderStatus`). The AhaMove webhook just
> needs to feed it the right `DeliveryStatus` — no new state machine required.

---

## 5. Wiring it into our Clean Architecture

Good news: the codebase is **already shaped for this**. We only add an Infrastructure implementation and (lightly)
extend the request model.

### 5.1 What already exists

| Layer | File | Role |
|---|---|---|
| Application (port) | `Interfaces/External/IShippingProvider.cs` | `CreateShipmentAsync(ShipmentRequest) → ShipmentResult` |
| Application (models) | `Interfaces/External/ShippingProviderModels.cs` | `ShipmentRequest`, `ShipmentResult` records |
| Application (webhook) | `Features/Shipping/Services/IShippingService.cs` | `ProcessWebhookAsync(ShippingWebhookRequest)` |
| Application (DTO) | `Features/Shipping/DTOs/ShippingDtos.cs` | `ShippingWebhookRequest` (normalized callback) |
| Infrastructure (impl) | `ExternalServices/Shipping/MockShopeeShippingProvider.cs` | current mock — **we replace/duplicate this** |
| WebAPI | `Controllers/ShippingController.cs` | `POST /api/shipping/webhook` with `X-Webhook-Secret` |
| Domain | `Entities/Sales/Delivery.cs` | already has `TrackingUrl`, `ProviderOrderId`, `ShippingProvider` |
| Domain | `Entities/Vendor/GardenStore.cs` | already has `AhamoveServiceId` |
| Domain | `Entities/Vendor/StoreAddress.cs` | pickup address + `SenderName` / `SenderPhone` + lat/lng |

The shipment is created today in `PaymentService.CreateShipmentsAsync(...)`: it loops over every `Delivery` with
status `Pending` and calls `_shipping.CreateShipmentAsync(...)`. **That loop is exactly our multi-vendor split** —
one AhaMove order per store. Keep it.

### 5.2 Extend `ShipmentRequest` (the one gap)

Today `ShipmentRequest` only carries recipient + a single address string. AhaMove needs **pickup** (store) info,
geo-coordinates, items and weight. Extend the record (Application layer, no new dependency):

```csharp
// Application/Interfaces/External/ShippingProviderModels.cs
public record ShipmentRequest(
    Guid DeliveryId,
    Guid OrderId,
    decimal Subtotal,
    // --- pickup (from StoreAddress) ---
    string PickupName,
    string PickupPhone,
    string PickupAddress,
    decimal? PickupLat,
    decimal? PickupLng,
    string ServiceId,            // GardenStore.AhamoveServiceId, e.g. "SGN-BIKE"
    // --- drop-off (customer) ---
    string RecipientName,
    string RecipientPhone,
    string ShippingAddress,
    decimal? RecipientLat,
    decimal? RecipientLng,
    decimal CodAmount,           // 0 when paid online
    int TotalWeightGram,
    IReadOnlyList<ShipmentItem> Items);

public record ShipmentItem(string Id, string Name, decimal Price, int Quantity);

public record ShipmentResult(
    string Provider,
    string ProviderOrderId,
    string TrackingCode,
    DateTime? EstimatedDeliveryDate,
    string? TrackingUrl);        // NEW: AhaMove shared_link
```

`PaymentService.CreateShipmentsAsync` then fills the pickup fields from each delivery's `Store.Address` and the
items from `delivery.Items`, and copies `shipment.TrackingUrl` into `delivery.TrackingUrl`.

> Keep the provider **DB-free**: the service (Application) gathers store + customer data and passes it in.
> The provider only talks HTTP. This respects the dependency rule (Domain ← Application ← Infrastructure).

### 5.3 New provider: `AhamoveShippingProvider`

Create `Infrastructure/ExternalServices/Shipping/AhamoveShippingProvider.cs` implementing `IShippingProvider`
(mirror the mock's shape). Use a **typed `HttpClient`** + a small token cache.

```csharp
public class AhamoveShippingProvider : IShippingProvider
{
    private readonly HttpClient _http;
    private readonly AhamoveSettings _cfg;
    private readonly IAhamoveTokenProvider _token;   // caches token, refreshes on 401
    private readonly ILogger<AhamoveShippingProvider> _logger;

    public string Name => "Ahamove";

    public async Task<ShipmentResult> CreateShipmentAsync(ShipmentRequest req, CancellationToken ct = default)
    {
        var body = new
        {
            order_time = 0,
            service_id = req.ServiceId,                 // "SGN-BIKE"
            payment_method = req.CodAmount > 0 ? "CASH_BY_RECIPIENT" : "BALANCE",
            path = new object[]
            {
                new { lat = req.PickupLat, lng = req.PickupLng, address = req.PickupAddress,
                      name = req.PickupName, mobile = req.PickupPhone },
                new { lat = req.RecipientLat, lng = req.RecipientLng, address = req.ShippingAddress,
                      name = req.RecipientName, mobile = req.RecipientPhone,
                      cod = req.CodAmount, item_value = req.Subtotal,
                      tracking_number = req.DeliveryId.ToString() }   // idempotency key
            },
            items = req.Items.Select(i => new { _id = i.Id, name = i.Name, price = i.Price, num = i.Quantity }),
            package_detail = new[] { new { weight = req.TotalWeightGram / 1000.0 } }
        };

        var token = await _token.GetAsync(ct);
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/v3/orders")
        {
            Content = JsonContent.Create(body)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _http.SendAsync(msg, ct);
        if (res.StatusCode == HttpStatusCode.Unauthorized)        // token expired
        {
            token = await _token.RefreshAsync(ct);
            // ... rebuild request with new token and retry once ...
        }
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<AhamoveCreateOrderResponse>(cancellationToken: ct);
        return new ShipmentResult(
            Provider: Name,
            ProviderOrderId: dto!.OrderId,
            TrackingCode: dto.OrderId,
            EstimatedDeliveryDate: DateTime.UtcNow.AddHours(3),
            TrackingUrl: dto.SharedLink);
    }
}
```

`IAhamoveTokenProvider` is a singleton that calls `POST /v3/accounts/token`, caches `{token}` in memory, and
refreshes on demand. Settings:

```csharp
// Infrastructure/ExternalServices/Shipping/AhamoveSettings.cs
public class AhamoveSettings
{
    public const string SectionName = "Ahamove";
    public string BaseUrl { get; set; } = null!;
    public string ApiKey { get; set; } = null!;
    public string Mobile { get; set; } = null!;
}
```

### 5.4 Register in DI (swap the mock)

In `Infrastructure/DependencyInjection.cs` (currently line ~175 registers `MockShopeeShippingProvider`):

```csharp
services.Configure<AhamoveSettings>(config.GetSection(AhamoveSettings.SectionName));
services.AddSingleton<IAhamoveTokenProvider, AhamoveTokenProvider>();
services.AddHttpClient<IShippingProvider, AhamoveShippingProvider>((sp, c) =>
{
    var cfg = sp.GetRequiredService<IOptions<AhamoveSettings>>().Value;
    c.BaseAddress = new Uri(cfg.BaseUrl);
});
// keep MockShopee for tests/local: pick via config flag if you want both.
```

A clean approach: keep `MockShopeeShippingProvider` and choose the implementation by a config flag
(`Shipping:Provider = "Ahamove" | "Mock"`) so local dev still works without AhaMove credentials.

---

## 6. Webhook (AhaMove callback → our `ProcessWebhookAsync`)

AhaMove calls **one URL you give them** on every status change, POSTing the full order JSON (see §3.3 / sample
below). They authenticate using a header you configure (API-Key, Bearer, or Basic). Our existing endpoint already
guards with `X-Webhook-Secret`, so:

**Option A (recommended):** add a thin AhaMove-specific endpoint that adapts AhaMove's payload into our normalized
`ShippingWebhookRequest`, then calls the same `IShippingService.ProcessWebhookAsync`. This keeps the core logic
(raw-save → resolve delivery → validate transition → progress log → order rollup → notification) untouched.

```csharp
// WebAPI/Controllers/ShippingController.cs  (new action)
[HttpPost("ahamove/webhook")]
[AllowAnonymous]
public async Task<IActionResult> AhamoveWebhook([FromBody] AhamoveCallback cb, CancellationToken ct)
{
    // verify the secret AhaMove sends (configure them to send X-Webhook-Secret or check cb.api_key)
    var provided = Request.Headers["X-Webhook-Secret"].ToString();
    if (string.IsNullOrEmpty(_settings.Secret) || provided != _settings.Secret)
        return Unauthorized(...);

    var req = new ShippingWebhookRequest
    {
        Provider        = "Ahamove",
        EventType       = cb.Status,                       // "IN PROCESS", "COMPLETED"...
        ProviderOrderId = cb.Id,                           // matches Delivery.ProviderOrderId
        // OR resolve by the tracking_number we sent (delivery.Id) from cb.path[1].tracking_number
        NewStatus       = MapAhamoveStatus(cb),            // see §4 table
        TrackingCode    = cb.Id,
        RawPayload      = JsonSerializer.Serialize(cb)
    };
    return ToActionResult(await _service.ProcessWebhookAsync(req, ct));
}
```

`MapAhamoveStatus` implements the §4 table (look at `cb.Status`, the drop-off `path[i].status`, and `cb.sub_status`).
`ResolveDeliveryAsync` in `ShippingService` already matches by `(Provider + ProviderOrderId)` — so set
`ProviderOrderId = cb.Id`. (Alternatively resolve by `DeliveryId` parsed from the `tracking_number` we sent.)

**Sample callback payload** (trimmed):

```jsonc
{
  "_id": "22VMV54W",                  // AhaMove order id  (= our ProviderOrderId)
  "status": "ASSIGNING",
  "sub_status": "",
  "service_id": "SGN-BIKE",
  "cancel_by_user": false,
  "shared_link": "https://express.ahamove.com/s/...",
  "path": [
    { "name": "Store",    "status": "" },
    { "name": "Customer", "status": "", "tracking_number": "<delivery.Id>" }  // drop-off result lands here
  ],
  "total_price": 33000
}
```

> The flow is **idempotent by design**: `ProcessWebhookAsync` saves the raw webhook first, and skips invalid
> transitions (logging them) — so AhaMove retries / duplicate callbacks are safe.

Webhook auth note: register your webhook URL with AhaMove (via the integration contact). They support
`apikey`, `Bearer`, or `Basic` auth headers — pick one and validate it in the controller instead of (or in addition
to) `X-Webhook-Secret`.

---

## 7. End-to-end multi-vendor flow

```
Customer pays order #1234 (items from Store A in Q10 + Store B in Bình Thạnh)
        │
        ▼  PaymentService (on Paid)
SplitDeliveries:  Delivery_A (Store A items)   Delivery_B (Store B items)
        │
        ▼  CreateShipmentsAsync  → loop per delivery
   ┌─────────────────────────────┐      ┌─────────────────────────────┐
   │ AhaMove order #1  (Store A)  │      │ AhaMove order #2  (Store B)  │
   │ pickup = Store A address     │      │ pickup = Store B address     │
   │ service_id = A.AhamoveServiceId     │ service_id = B.AhamoveServiceId
   │ tracking_number = Delivery_A.Id     │ tracking_number = Delivery_B.Id
   └─────────────┬───────────────┘      └──────────────┬──────────────┘
                 │ shared_link → Delivery_A.TrackingUrl │ shared_link → Delivery_B.TrackingUrl
                 ▼                                       ▼
         AhaMove callbacks  ───►  POST /api/shipping/ahamove/webhook  ───►  ProcessWebhookAsync
                 │                                                              │
                 ▼                                                              ▼
        Delivery_A.Status updated                              OrderWorkflow.ComputeOrderStatus
        + progress log + customer notification                 rolls Order #1234 status up
```

Key multi-vendor rules:

- **One `Delivery` ⇒ one AhaMove order.** Never merge stores into a single courier order — different pickups.
- **Shipping fee is per delivery.** Today `ShippingFeeCalculator` computes a deterministic fee. With AhaMove you
  can instead (or additionally) call **Estimate (§3.2) per delivery** to show the real courier fee, then store it
  on `Delivery.ShippingFee`. Decide one source of truth so the customer total is consistent.
- **The parent `Order` only completes when all deliveries reach a terminal state** — already handled by
  `OrderWorkflow.ComputeOrderStatus`.
- **Each store needs valid pickup data**: `StoreAddress` (street + ward + lat/lng) and a real mobile in
  `SenderPhone` (the store `Hotline` may be a 1900 number AhaMove rejects), plus `AhamoveServiceId` set to the
  store-city service (e.g. `SGN-BIKE`).

---

## 8. Notes, limits & fallbacks

- **Same-city only.** AhaMove BIKE/ECO are intra-city. If a store and customer are in different provinces, a
  create/estimate call returns `INVALID_MAX_DISTANCE` or `INVALID_*_AREA`. Plan a fallback provider (the
  `IShippingProvider` abstraction lets you add e.g. a GHN provider and route per delivery by distance/area).
- **Address format matters.** AhaMove validates addresses (must be Google-Maps-searchable, comma-separated:
  `house, street, ward, district, city`). Send `lat`/`lng` from `StoreAddress` whenever available to reduce failures.
- **COD vs online:** prefer `BALANCE` (we collect via PayOS). Only use `CASH_BY_RECIPIENT` + `cod` for true COD orders.
- **Cancellation window:** cancel only before `IN PROCESS`. After pickup, handle via the Returns flow, not cancel.
- **Money:** never auto-trigger payouts or wallet top-ups from code without an explicit business step.
- **Still mock today:** the repo ships `MockShopeeShippingProvider`; AhaMove is not yet implemented. The hooks
  (`AhamoveServiceId`, `TrackingUrl`, `IShippingProvider`, webhook pipeline) are already in place — this guide is
  the plan to fill them in.

---

## 9. Implementation checklist

**Code (done — see commit wiring AhaMove into the `Shipping` context):**

- [x] Add `AhamoveSettings` + config section (`Ahamove` + `Shipping:Provider`); secrets via env / `.env`.
- [x] Build `IAhamoveTokenProvider` / `AhamoveTokenProvider` (singleton cache, refresh on 401, `IHttpClientFactory`).
- [x] Extend `ShipmentRequest` / `ShipmentResult` with pickup + drop-off geo + items + `TrackingUrl` (§5.2).
- [x] Implement `AhamoveShippingProvider : IShippingProvider` (typed HttpClient, 401-refresh-retry) (§5.3).
- [x] Fill pickup/items + create shipment at all 3 points; copy `TrackingUrl` to `Delivery`:
  - **Online (PayOS):** `PaymentService.CreateShipmentsAsync` khi nhận thanh toán.
  - **COD:** `OrderService.UpdateDeliveryStatusAsync` khi store xác nhận (`Pending → Confirmed`, chỉ tạo nếu chưa có `ProviderOrderId`).
  - **Đổi hàng:** `ReturnService` khi tạo delivery thay thế (COD = 0).
  - Dùng chung `ShipmentRequestBuilder` + `ShipmentAddressFormatter`.
- [x] DI: select provider by `Shipping:Provider = "Ahamove" | "Mock"` (default Mock) (§5.4).
- [x] Add `POST /api/shipping/ahamove/webhook` adapter → `ProcessWebhookAsync`; set `TrackingUrl` on webhook (§6).
- [x] Implement `AhamoveStatusMapper.Map` per the §4 table; resolve delivery by `tracking_number` (delivery.Id) or `ProviderOrderId`.

**Ops / business (still to do — needs real credentials & data):**

- [ ] Register partner account on AhaMove staging, get `API_KEY_STG` + partner mobile; set `Ahamove__ApiKey`, `Ahamove__Mobile`, `Shipping__Provider=Ahamove`.
- [ ] (Optional) call Estimate per delivery to set `Delivery.ShippingFee`.
- [ ] Register webhook URL + auth with AhaMove; configure `ShippingWebhook:Secret` (or adapt to AhaMove's apikey/Bearer header).
- [ ] Ensure every active `GardenStore` has `Address` (lat/lng, valid `SenderPhone`) + `AhamoveServiceId`.
- [ ] Run AhaMove UAT test cases with the AhaDriver STG app; submit UAT form; golive.

---

## Sources

- [AhaMove Developers — Introduction](https://developers.ahamove.com/en/docs/introduction)
- [Overall Process](https://developers.ahamove.com/en/docs/overall-process)
- [Account APIs — Register / Authenticate token](https://developers.ahamove.com/en/docs/api-reference/account-apis)
- [Estimate Order Fee](https://developers.ahamove.com/en/docs/api-reference/order-apis/estimate-order-fee)
- [Create Order](https://developers.ahamove.com/en/docs/api-reference/order-apis/create-order)
- [Cancel Order](https://developers.ahamove.com/en/docs/api-reference/order-apis/cancel-order)
- [Order Status Flow](https://developers.ahamove.com/en/docs/order-status-flow)
- [Webhook & Callback flow](https://developers.ahamove.com/en/docs/webhook)
