# Fix: Tính trước phí ship (GHN) ở trang Checkout

## Kết luận
**Lỗi ở FE — FE chưa gọi API.** BE đã hoàn chỉnh: có endpoint, gọi GHN `/fee`, có fallback khi GHN lỗi.
Việc cần làm là wiring phía FE để gọi `POST /api/orders/shipping-fee-preview` và hiển thị phí.

---

## Flow tổng quan
```
FE CheckoutPage ──POST /api/orders/shipping-fee-preview──► OrdersController
                                                                │
                                              OrderService.PreviewShippingFeeAsync
                                                                │
                                              DeliveryFeeEstimator.EstimateAsync
                                                   │ (ưu tiên)        │ (fallback)
                                              GHN /fee API      ShippingFeeCalculator nội bộ
```

---

## Vị trí các API / file liên quan

### Backend (đã có sẵn)
| Thành phần | File | Dòng |
|---|---|---|
| Endpoint preview | `FengDeskAI/src/FengDeskAI.WebAPI/Controllers/OrdersController.cs` | 33–35 |
| Service logic | `FengDeskAI/src/FengDeskAI.Application/Features/Sales/Services/OrderService.cs` | `PreviewShippingFeeAsync` (359) |
| Tính phí từng store | cùng file trên | `ComputeStoreFeesAsync` (~256) |
| Ước tính + fallback | `.../Features/Shipping/Services/DeliveryFeeEstimator.cs` | `EstimateAsync` |
| Gọi GHN /fee | `.../Infrastructure/ExternalServices/Shipping/GhnShippingProvider.cs` | `EstimateFeeAsync` (61) |
| DTO request | `.../Features/Sales/DTOs/OrderDtos.cs` | `CheckoutRequest` (12) |
| DTO response | `.../Features/Sales/DTOs/OrderDtos.cs` | `ShippingFeePreviewResponse` (31) |

**Endpoint:** `POST /api/orders/shipping-fee-preview`
**Body:** `{ shippingAddressId, items: [{ productItemId, quantity }] }` (giống checkout, không cần `paymentMethod`)
**Response:** `{ subtotal, totalShippingFee, totalAmount, stores: [{ storeId, storeName, subtotal, shippingFee }] }`

### Frontend (CẦN sửa)
| Thành phần | File |
|---|---|
| API service đơn hàng | `FengDeskAI_FE/src/features/orders/api/orders.api.ts` |
| Type định nghĩa | `FengDeskAI_FE/src/features/orders/types/orders.d.ts` |
| Hook react-query | `FengDeskAI_FE/src/features/orders/hooks/useOrders.ts` |
| Trang checkout | `FengDeskAI_FE/src/features/orders/pages/CheckoutPage.tsx` |

---

## Các bước sửa FE

### 1) Thêm type — `features/orders/types/orders.d.ts`
```ts
// Request gửi lên preview (giống checkout nhưng không cần paymentMethod)
export interface PreviewShippingFeePayload {
  shippingAddressId: string;
  items: OrdersItem[];
}

// Phí theo từng store
export interface StoreShippingFee {
  storeId: string;
  storeName: string;
  subtotal: number;
  shippingFee: number;
}

// Response preview
export interface ShippingFeePreview {
  subtotal: number;
  totalShippingFee: number;
  totalAmount: number;
  stores: StoreShippingFee[];
}
```

### 2) Thêm method gọi API — `features/orders/api/orders.api.ts`
```ts
import type {
  // ...các type cũ...
  ApiResponse,
  PreviewShippingFeePayload,
  ShippingFeePreview,
} from "../types/orders";

export const ordersApi = {
  // ...các method cũ...

  // POST /api/orders/shipping-fee-preview
  previewShippingFee: (payload: PreviewShippingFeePayload) => {
    return fetchHttpClient.post<ApiResponse<ShippingFeePreview>>(
      "/orders/shipping-fee-preview",
      payload,
    );
  },
};
```

### 3) Thêm hook — `features/orders/hooks/useOrders.ts`
Dùng `useQuery` để tự gọi lại khi đổi địa chỉ hoặc sản phẩm.
```ts
import type { OrdersItem } from "../types/orders";

export function useShippingFeePreview(
  shippingAddressId: string | undefined,
  items: OrdersItem[],
) {
  const query = useQuery({
    queryKey: ["shipping-fee-preview", shippingAddressId, items],
    enabled: Boolean(shippingAddressId) && items.length > 0, // chỉ gọi khi đủ dữ liệu
    queryFn: async () => {
      const res = await ordersApi.previewShippingFee({
        shippingAddressId: shippingAddressId!,
        items,
      });
      return res.data; // ApiResponse<ShippingFeePreview>
    },
  });

  const preview = query.data?.isSuccess ? query.data.data : undefined;

  return {
    shippingFee: preview?.totalShippingFee ?? 0,
    totalAmount: preview?.totalAmount,
    stores: preview?.stores ?? [],
    isLoading: query.isLoading || query.isFetching,
    isError: query.isError,
  };
}
```
> Nhớ export hook trong `features/orders/index.ts` nếu file đó re-export.

### 4) Wiring trang checkout — `features/orders/pages/CheckoutPage.tsx`

**a. Import hook (đầu file, cạnh `useCreateOrder`):**
```ts
import { useCreateOrder, useShippingFeePreview } from "@/features/orders";
```

**b. Gọi hook (sau khi đã có `selectedAddressId` và `checkoutItems`, ~dòng 40):**
```ts
const previewItems = useMemo(
  () =>
    checkoutItems.map((item) => ({
      productItemId: item.productItemId,
      quantity: item.quantity,
    })),
  [checkoutItems],
);

const {
  shippingFee,
  isLoading: feeLoading,
} = useShippingFeePreview(selectedAddressId, previewItems);
```

**c. Sửa khối hiển thị (hiện ở dòng 286–298):**
```tsx
<div className="space-y-2 border-t border-dashed border-gray-200 pt-4 text-sm">
  <div className="flex justify-between text-gray-600">
    <span>Tạm tính ({totalQuantity} sản phẩm)</span>
    <span className="font-semibold text-gray-900">{formatVnd(subtotal)}</span>
  </div>
  <div className="flex justify-between text-gray-600">
    <span>Phí vận chuyển</span>
    <span className="font-semibold text-gray-900">
      {feeLoading ? "Đang tính..." : formatVnd(shippingFee)}
    </span>
  </div>
  <div className="flex justify-between border-t border-gray-100 pt-3 text-base font-bold text-gray-900">
    <span>Tổng cộng</span>
    <span className="text-primary">
      {feeLoading ? "..." : formatVnd(subtotal + shippingFee)}
    </span>
  </div>
</div>
```

**Thay đổi cốt lõi:**
- `"Chưa tính"` → `formatVnd(shippingFee)` (có trạng thái "Đang tính...").
- Tổng cộng: `formatVnd(subtotal)` → `formatVnd(subtotal + shippingFee)`.

---

## Checklist kiểm thử
- [ ] Đổi địa chỉ giao → phí ship gọi lại và cập nhật.
- [ ] Đổi sản phẩm/số lượng → phí ship cập nhật.
- [ ] GHN lỗi/sập → vẫn ra phí (BE fallback calculator), checkout không vỡ.
- [ ] Tổng cộng = Tạm tính + Phí vận chuyển.
- [ ] Khi chưa chọn địa chỉ → không gọi API (hook `enabled` = false).
```
