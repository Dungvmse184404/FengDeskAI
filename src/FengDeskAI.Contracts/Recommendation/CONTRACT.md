# AI Recommendation Contract

Hợp đồng giữa **.NET monolith** (caller) và **AI microservice** (Python, scorer/explainer).

## Luồng

1. .NET chấm điểm deterministic (engine phong thủy) → chọn top-N sản phẩm.
2. .NET gửi `AiRecommendationRequest` sang AI.
3. AI trả `AiRecommendationResponse` (diễn giải + thứ tự cuối).
4. .NET lưu kết quả, audit chênh lệch `BaseRank` ↔ `FinalRank`.

## LUẬT bắt buộc AI tuân thủ

1. **Không thêm/bớt sản phẩm.** Tập `Items` trả về phải đúng bằng tập `Candidates` nhận vào (so theo `ProductId`). Vi phạm → .NET bỏ qua phần thừa, giữ thứ hạng engine cho phần thiếu.
2. **Chỉ diễn giải dựa trên `MatchFacts`/`CautionFacts`.** KHÔNG bịa thêm luật phong thủy. Mệnh, hướng, ngũ hành đều do .NET tính.
3. **Chỉ được hoán vị thứ tự trong tập đã nhận** (`FinalRank`). Nên bám sát `BaseRank`, chỉ đảo khi có lý do từ facts.
4. **Tôn trọng `CautionFacts`.** Nếu có cảnh báo (vd "khắc bản mệnh", "quá khổ"), diễn giải phải trung thực, không lờ đi.
5. **`Customer.Element == null`** nghĩa là đã bỏ qua yếu tố cá nhân (giới tính không Nam/Nữ) → AI chỉ nhấn công năng (mục đích/ánh sáng/kích thước), không suy đoán mệnh.

## Enum (closed-set, gửi dạng string)

- `Element`, ngũ hành: `Kim | Moc | Thuy | Hoa | Tho`
- `KuaGroup`: `East | West`
- `DeskOrientation`, `FavorableDirections`: `North | Northeast | East | Southeast | South | Southwest | West | Northwest`
- `Purpose`: `Office | Study | Creative | Reading | Gaming | Mixed | Other`
- `Style`: `Modern | Classic | Minimal | Industrial | Scandinavian | Bohemian | Other`
- `Lighting`: `Natural | Artificial | Mixed | Dim`
