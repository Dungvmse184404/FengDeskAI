# Fix triệt để — Lưu / sửa địa chỉ cửa hàng không xuống DB

> Triệu chứng: Tạo/sửa địa chỉ cửa hàng trong `ManageStoresPage` bấm "Lưu" nhưng không xuống DB (đặc biệt khi **sửa** địa chỉ đã có).

---

## 1. Nguyên nhân (đã xác minh trong code)

Bug **không** phải nối nhầm sang API customer — FE gọi đúng `/stores/{id}/address`. Vấn đề nằm ở **lệch hợp đồng FE↔BE** + **đọc sai field response**:

**a) Đọc sai tên field địa chỉ trong response** — `ManageStoresPage.tsx` (dòng 345, 427):
```js
const existingAddress = selectedStoreDetails?.storeAddress || selectedStoreDetails?.addressEntity || null;
```
BE trả địa chỉ ở field **`address`** (object `StoreAddressResponse`), không phải `storeAddress`/`addressEntity` → `existingAddress` **luôn null**.
→ Nhánh update **không bao giờ chạy**; luôn gọi **POST create**. Khi store đã có địa chỉ (1-1), BE `AddAddressAsync` trả **`409 Conflict "AlreadyExists"`** (`StoreService.cs:143`) → **không lưu**. ⇒ chính là "sửa không xuống DB".
→ Prefill form khi mở edit cũng đọc sai key → form trống.

**b) Form/DTO địa chỉ store bị dựng theo địa chỉ customer** — `shop.d.ts:45-77` + form state có `recipientName`, `recipientPhone`, `isDefault`, `label`; validation **bắt buộc** `recipientName`/`recipientPhone` (`ManageStoresPage.tsx:544-546` + `required` trong modal).
BE store address **KHÔNG có** các field này. `CreateStoreAddressRequest` chỉ gồm `wardId`, `streetAddress`, `latitude`, `longitude` (`StoreService.cs:154-158`). ⇒ field customer bị BE bỏ qua, và required thừa gây rối/chặn nhầm.

---

## 2. Nguồn sự thật — Hợp đồng BE (chốt theo đây)

`StoreAddress` (entity) & `CreateStoreAddressRequest` / `UpdateStoreAddressRequest`:
```
wardId       : Guid   (bắt buộc, phải tồn tại trong wards)
streetAddress: string (bắt buộc)
latitude     : decimal?  (tùy chọn)
longitude    : decimal?  (tùy chọn)
```
Response `StoreAddressResponse`: `id, storeId, wardId, streetAddress, latitude, longitude, isActive`.
Endpoints: `POST /stores/{id}/address` (tạo, 409 nếu đã có), `PUT /stores/{id}/address` (sửa), `DELETE .../address` (soft), `DELETE .../address/hard`.

> **Quyết định thiết kế (khuyến nghị):** store address **không cần** `recipientName/recipientPhone/isDefault/label`. Cửa hàng đã có `name` + `hotline`; địa chỉ store là điểm lấy hàng (pickup), không cần "người nhận". → **Bỏ** các field customer ở FE. (Nếu nhóm vẫn muốn có tên/SĐT liên hệ kho → phải sửa BE, xem §5.)

---

## 3. Fix Frontend (bản triệt để)

### F1. Types — cắt đúng hợp đồng BE
`features/shop/types/shop.d.ts`:
```ts
export interface StoreAddress {
  id: string;
  storeId: string;
  wardId: string;
  streetAddress: string;
  latitude: number | null;
  longitude: number | null;
  isActive: boolean;
}

export interface CreateStoreAddressDto {
  wardId: string;
  streetAddress: string;
  latitude?: number | null;
  longitude?: number | null;
}
export type UpdateStoreAddressDto = CreateStoreAddressDto;
```
> Bỏ hẳn `recipientName`, `recipientPhone`, `isDefault`, `label` khỏi store address.

### F2. Một helper duy nhất cho save (POST vs PUT theo tồn tại)
`ManageStoresPage.tsx` — thay 2 nhánh rải rác bằng 1 hàm, và **đọc đúng field `address`**:
```ts
// payload chỉ 4 field BE nhận
function toStoreAddressPayload(f: AddressFormState): CreateStoreAddressDto {
  return {
    wardId: f.wardId,
    streetAddress: f.streetAddress.trim(),
    latitude: f.latitude || null,
    longitude: f.longitude || null,
  };
}

async function saveStoreAddress(storeId: string, existingAddressId: string | undefined,
                                f: AddressFormState) {
  const payload = toStoreAddressPayload(f);
  return existingAddressId
    ? updateShopAddressRequest(storeId, payload)   // PUT khi đã có
    : createShopAddressRequest(storeId, payload);  // POST khi chưa có
}
```
Lấy địa chỉ hiện có từ **`address`** (không phải `storeAddress`/`addressEntity`):
```ts
const existingAddr = (selectedStoreDetails as any)?.address ?? null;   // object hoặc null
// ...
await saveStoreAddress(storeId, existingAddr?.id, addressForm);
```

### F3. Prefill khi mở edit — đọc đúng field
`handleOpenAddressModal` / chỗ nạp form (dòng ~345, ~509):
```ts
const a = (selectedStoreDetails as any)?.address ?? addr ?? null;
setAddressForm({
  wardId: a?.wardId ?? "",
  streetAddress: a?.streetAddress ?? "",
  latitude: a?.latitude ?? 10.8231,
  longitude: a?.longitude ?? 106.6297,
});
```

### F4. Bỏ validation & input customer
- `handleSubmitAddress` (dòng 543-549): chỉ còn check `wardId` + `streetAddress`:
```ts
if (!addressForm.wardId || !addressForm.streetAddress.trim()) {
  toast.error("Vui lòng chọn khu vực và nhập số nhà/đường");
  return;
}
```
- `AddressFormState`: chỉ `{ wardId, streetAddress, latitude, longitude }`.
- `StoreAddressModal.tsx`: **xoá** 2 block input "Tên người liên hệ" + "Số điện thoại liên hệ" (dòng 78-105) và bỏ chúng khỏi `AddressFormState`.
- `hasAddressInput` (dòng 408-416): bỏ điều kiện `recipientName`/`recipientPhone`, chỉ dựa `wardId || streetAddress || lat/lng`.

### F5. Sau khi lưu → refetch
Invalidate query chi tiết store (`['store', id]`) / `['my-shops']` để card hiện địa chỉ mới ngay.

---

## 4. Kiểm thử

- [ ] Store **chưa** có địa chỉ → nhập → Lưu → POST `201` → DB có bản ghi; card hiện địa chỉ.
- [ ] Store **đã** có địa chỉ → mở edit (form **prefill** đúng) → sửa → Lưu → **PUT `200`** (không còn 409) → DB cập nhật.
- [ ] Bỏ trống ward hoặc street → chặn ở FE, không gọi API.
- [ ] Không còn gửi `recipientName/recipientPhone/isDefault/label` trong request payload (kiểm Network tab).
- [ ] Xoá địa chỉ (soft) rồi tạo lại → BE hồi sinh bản ghi (không lỗi unique).

---

## 5. (Chỉ khi nhóm MUỐN giữ tên/SĐT liên hệ kho) — sửa BE

Nếu quyết định store address cần `recipientName` + `recipientPhone`:
- `StoreAddress` entity + migration: thêm 2 cột.
- `CreateStoreAddressRequest` / `UpdateStoreAddressRequest`: thêm 2 field.
- `StoreService.AddAddressAsync/UpdateAddressAsync`: set 2 field.
- `StoreAddressResponse` + mapping: trả 2 field.
- Giữ lại 2 input ở FE.
→ **Không khuyến nghị** cho Review (thừa so với `name`+`hotline` của store). Mặc định chọn **§3 (bỏ field)**.

---

## 6. Checklist

**Frontend**
- [ ] `shop.d.ts`: `StoreAddress`/`CreateStoreAddressDto`/`UpdateStoreAddressDto` = 4 field BE.
- [ ] `ManageStoresPage`: helper `saveStoreAddress` + đọc field `address`; POST/PUT theo `address.id`.
- [ ] Prefill edit đọc `selectedStoreDetails.address`.
- [ ] Bỏ input + validation `recipientName/recipientPhone`; `AddressFormState` gọn.
- [ ] `StoreAddressModal`: xoá 2 input liên hệ.
- [ ] Invalidate query sau khi lưu.

**Verify**
- [ ] Tạo mới: POST 201. Sửa: PUT 200 (hết 409). Payload chỉ 4 field.

**Nguyên tắc**
- Store address = pickup point: `wardId + streetAddress + lat/lng`. Không tái dùng shape địa chỉ customer.
- Quyết định update/create dựa trên **`store.address.id`** (đọc đúng field response), không đoán bằng key sai.
