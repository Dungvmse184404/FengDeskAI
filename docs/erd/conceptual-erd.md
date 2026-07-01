# Conceptual ERD — FengDeskAI

> Dùng để đối chiếu khi sửa trang **"Conceptual diagram"** trong `SEP490_FengDeskAI.drawio`.
>
> Phạm vi: **chỉ thực thể dữ liệu + quan hệ**. KHÔNG vẽ external system (PayOS/GHN/Meshy/AI/SMTP/Storage — để dành cho **Context Diagram**), KHÔNG vẽ actor dạng người. Các vai trò (Customer/GardenOwner/Staff/Manager/Admin) gộp vào 1 entity **User** (phân biệt bằng role).
>
> Mức conceptual: chỉ tên entity + quan hệ + cardinality, **không liệt kê thuộc tính/khóa** (phần đó thuộc Logical ERD).

---

## 0. Phiên bản RÚT GỌN (khuyến nghị cho conceptual)

> Bản gọn ~17 entity — đủ kể câu chuyện nghiệp vụ, dễ trình bày & bảo vệ. Các lookup (Province/District/Ward, Element/Style/Vibe/Tag), bảng trung gian (Cart Item, Order Item, Return Item...), bảng nối (StoreOwner/StoreStaff) và chi tiết phụ (3D Model, Image) **để dành cho Logical ERD**. Quan hệ N–N vẽ thẳng.

### Entity (17)
```
User, Address, Garden Store, Product,
Workspace Profile, Workspace Type, Recommendation, Feng Shui Rule,
Cart, Order, Delivery, Payment,
Return Request, Refund, Review,
Conversation, Notification
```

### Quan hệ
```
User (1) ——— has ———> (N) Address
User (1) ——— owns ———> (N) Workspace Profile
Workspace Profile (N) ——— is of type ———> (1) Workspace Type
User (1) ——— requests ———> (N) Recommendation
Recommendation (N) ——— based on ———> (1) Workspace Profile
Recommendation (N) ——— suggests ———> (N) Product
Recommendation (1) ——— applies ———> (N) Feng Shui Rule

User (N) ——— owns / works at ———> (N) Garden Store
Garden Store (1) ——— sells ———> (N) Product

User (1) ——— owns ———> (1) Cart
Cart (N) ——— contains ———> (N) Product        [ghi chú: quantity]
User (1) ——— places ———> (N) Order
Order (N) ——— contains ———> (N) Product        [ghi chú: quantity, unitPrice]
Order (1) ——— ships to ———> (1) Address
Order (1) ——— split into ———> (N) Delivery
Delivery (N) ——— fulfilled by ———> (1) Garden Store
Order (1) ——— paid by ———> (1) Payment

User (1) ——— requests ———> (N) Return Request
Return Request (N) ——— against ———> (1) Delivery
Return Request (1) ——— results in ———> (0..1) Refund
User (1) ——— writes ———> (N) Review
Review (N) ——— about ———> (1) Product

User (N) ——— participates in ———> (N) Conversation
User (1) ——— receives ———> (N) Notification
```

> Nếu giảng viên yêu cầu chi tiết hơn (conceptual = logical bỏ thuộc tính), dùng **bản đầy đủ** ở mục 1–3 bên dưới.

---

## 1. Danh sách Entity (bản đầy đủ — gộp theo nhóm)

### Người dùng & vị trí
```
h chính (Address tham chiếu tới Ward)
```

### Không gian làm việc & gợi ý phong thủy
```
Workspace Profile – Hồ sơ không gian làm việc của User (input cho gợi ý)
Workspace Type    – Loại không gian (quyết định trọng số gợi ý)
Recommendation    – Phiên gợi ý sinh cho 1 Workspace Profile
Recommendation Item – Dòng gợi ý (Recommendation ↔ Product kèm score/rank)
Feng Shui Rule    – Luật tương sinh/tương khắc ngũ hành (engine dùng chấm điểm)
```

### Cửa hàng & sản phẩm (catalog)
```
Garden Store      – Cửa hàng (marketplace)
Product           – Sản phẩm
Product Item      – Biến thể/SKU của Product (giá + tồn kho riêng)
Category          – Danh mục sản phẩm
Tag               – Nhãn sản phẩm
Feng Shui Attribute – Thuộc tính phong thủy của Product (Element / Style / Vibe / SizeClass)
Product 3D Model  – Model 3D sinh từ ảnh sản phẩm
```

### Mua hàng – thanh toán – giao hàng
```
Cart              – Giỏ hàng (1-1 với User)
Cart Item         – Dòng trong giỏ (Cart ↔ Product Item)
Order             – Đơn hàng
Order Item        – Dòng sản phẩm trong Order
Delivery          – Gói giao hàng (1 Order tách theo từng Garden Store)
Payment           – Giao dịch thanh toán của Order
```

### Sau bán hàng
```
Return Request    – Yêu cầu trả/đổi (RMA) gắn với 1 Delivery
Return Item       – Dòng sản phẩm trong Return Request
Refund            – Khoản hoàn tiền sinh từ Return Request
Review            – Đánh giá sản phẩm của User
```

### Giao tiếp
```
Conversation      – Phòng chat (người↔người hoặc người↔AI)
Message           – Tin nhắn trong Conversation
Notification      – Thông báo gửi tới User
```

---

## 2. Cần SỬA trên diagram hiện tại

```
BỎ   – "Report" (code chưa có; nếu là tính năng tương lai thì ghi rõ "future")
BỎ   – các box external: "3D AI API", "Shipper (3rd Party Logistics)", Webhook/callback
        → chuyển sang Context Diagram
GỘP  – "Customer / Garden Owner / Garden Staff / Staff / Manager / Admin" → 1 entity "User"
TÁCH – "Feng Shui Rule" giữ là LUẬT cho engine; thêm "Feng Shui Attribute" gắn vào Product
THÊM – các entity ở mục 1 còn thiếu: Cart, Order Item, Delivery, Payment, Return Request,
        Refund, Review, Notification, Conversation, Message, Category, Workspace Type, Address...
ĐỔI – các quan hệ mơ hồ "Modify / Create / view / base on" → động từ rõ nghĩa (mục 3)
```

---

## 3. Quan hệ (Entity —động từ→ Entity, kèm cardinality)

### Người dùng & vị trí
```
User (1) ——— has ———> (N) Address
Address (N) ——— located at ———> (1) Ward
Ward (N) ——— belongs to ———> (1) District
District (N) ——— belongs to ———> (1) Province
```

### Không gian làm việc & gợi ý
```
User (1) ——— owns ———> (N) Workspace Profile
Workspace Profile (N) ——— is of type ———> (1) Workspace Type
User (1) ——— requests ———> (N) Recommendation
Recommendation (N) ——— based on ———> (1) Workspace Profile
Recommendation (1) ——— contains ———> (N) Recommendation Item
Recommendation Item (N) ——— refers to ———> (1) Product
Recommendation (1) ——— applies ———> (N) Feng Shui Rule
```

### Cửa hàng & sản phẩm
```
User (N) ——— owns ———> (N) Garden Store          [qua bảng đồng sở hữu]
User (N) ——— works at ———> (N) Garden Store        [qua phân công staff]
Garden Store (1) ——— sells ———> (N) Product
Product (1) ——— has ———> (N) Product Item
Product (N) ——— classified by ———> (N) Category
Product (N) ——— tagged with ———> (N) Tag
Product (1) ——— described by ———> (N) Feng Shui Attribute
Product (1) ——— has ———> (0..1) Product 3D Model
```

### Mua hàng – thanh toán – giao hàng
```
User (1) ——— owns ———> (1) Cart
Cart (1) ——— contains ———> (N) Cart Item
Cart Item (N) ——— refers to ———> (1) Product Item
User (1) ——— places ———> (N) Order
Order (1) ——— has ———> (N) Order Item
Order Item (N) ——— refers to ———> (1) Product Item
Order (1) ——— ships to ———> (1) Address
Order (1) ——— split into ———> (N) Delivery
Delivery (N) ——— fulfilled by ———> (1) Garden Store
Order (1) ——— paid by ———> (1) Payment
```

### Sau bán hàng
```
User (1) ——— requests ———> (N) Return Request
Return Request (N) ——— against ———> (1) Delivery
Return Request (1) ——— has ———> (N) Return Item
Return Item (N) ——— refers to ———> (1) Order Item
Return Request (1) ——— results in ———> (0..1) Refund
User (1) ——— writes ———> (N) Review
Review (N) ——— about ———> (1) Product
```

### Giao tiếp
```
User (N) ——— participates in ———> (N) Conversation
Conversation (1) ——— has ———> (N) Message
Message (N) ——— sent by ———> (1) User        [hoặc AI/System]
User (1) ——— receives ———> (N) Notification
```

---

## 4. Lưu ý format conceptual

- Mỗi entity = 1 box, chỉ ghi **tên** (không cột/thuộc tính).
- Quan hệ = đường nối có **nhãn động từ**; cardinality (1, N, 0..1) ghi ở 2 đầu.
- Các quan hệ **N–N** (User↔Store, Product↔Category, Product↔Tag, User↔Conversation): ở conceptual có thể nối thẳng N–N; xuống Logical ERD mới tách bảng trung gian.
- Associative entity (Cart Item, Order Item, Return Item, Recommendation Item) giữ lại vì mang dữ liệu riêng (quantity, score...).
