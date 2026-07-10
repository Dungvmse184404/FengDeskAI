# Workspace Element Insights v2 — Thiết kế trước khi code

> Redesign khối "Ngũ hành không gian của bạn" trên trang workspace profile:
> (1) vòng % cạnh tên workspace đổi từ *% hoàn thiện hồ sơ* → **% tương thích**,
> (2) 3 nhận định chuyển từ logic FE nghèo → **BE sinh structured insights** theo 3 case A/B/C,
> có ngữ nghĩa hành theo mục đích phòng và hành động khắc phục cụ thể.

## 0. Hiện trạng (đã khảo sát)

- `GET workspaces/{id}/element-analysis` → `WorkspaceElementAnalysisResponse { DominantNeed, Elements[] (Ideal/AdjustedIdeal/Current/Gap) }` — data thật từ engine v3 (`WorkspaceElementAnalyzer.Analyze`).
- 3 nhận định hiện do FE tự suy (`SpaceInsightList.tsx`) từ dấu của Gap — sẽ bỏ logic này.
- Vòng % hiện là `CompletenessPercent` (điền x/9 field) — data thật nhưng sai ý nghĩa mong muốn.
- "Gợi ý bổ sung" (`MissingFieldHints`) — data thật, GIỮ NGUYÊN, không thuộc scope này.

## 1. % Tương thích (CompatibilityPercent)

Đo mức "phòng đang đúng chuẩn lý tưởng của chính nó (đã điều chỉnh theo mục đích + bản mệnh)".

```
sumAbsGap = Σ |gap_e|  với e ∈ {Kim, Mộc, Thủy, Hỏa, Thổ}   // gap = adjustedIdeal − current
raw       = 1 − sumAbsGap / 2          // 2 = khác biệt tối đa giữa 2 phân phối Σ=1
compat%   = round(100 × raw)
```

- Cả 2 vector đều Σ=1 nên `sumAbsGap ∈ [0, 2]` → công thức tự chuẩn hóa, không cần magic number.
- Tính trong `GetElementAnalysisAsync`, thêm field `CompatibilityPercent` vào response.
- FE: `CompletenessRing` đổi nguồn sang `compatibilityPercent` (lấy từ element-analysis),
  tooltip đổi thành "Tương thích ngũ hành X%". `CompletenessPercent` + hints vẫn trả như cũ
  (dùng cho modal edit / chỗ khác nếu cần).
- Tùy chọn calibrate sau nếu điểm dồn cục 70–90%: `raw' = raw^1.5` — ghi chú, chưa làm.

## 2. Insights 3 dòng — sinh ở BE

### 2.1. DTO

```csharp
// Thêm vào WorkspaceElementAnalysisResponse
public SpaceInsights Insights { get; init; }

public sealed record SpaceInsights(string Case, IReadOnlyList<SpaceInsightLine> Lines);
// Case: "Imbalanced" (A) | "Balanced" (B) | "Toxic" (C)

public sealed record SpaceInsightLine(string Kind, string Title, string Text);
// Kind: "status" | "detail" | "action"  → FE map icon theo Kind, không hardcode 3 title cũ
```

### 2.2. Phân loại case (ngưỡng trên Gap)

```
EPSILON  = 0.05   // |gap| ≤ ε coi như cân bằng
STRONG   = 0.10   // thừa mạnh

deficits  = { e | gap_e >  EPSILON }   // thiếu
surpluses = { e | gap_e < -EPSILON }   // thừa

Case C (Toxic):  ∃ X ∈ surpluses, gap_X ≤ −STRONG, và X KHẮC Y với
                 Y ∈ deficits HOẶC Y là hành chủ đạo của mục đích phòng
                 (lấy từ WorkPurposeElementModifier có boost dương lớn nhất).
Case B (Balanced): deficits = ∅ và surpluses = ∅.
Case A (Imbalanced): còn lại.
```

Vòng khắc: Kim→Mộc, Mộc→Thổ, Thổ→Thủy, Thủy→Hỏa, Hỏa→Kim.
Vòng sinh (dùng cho tiết khí): Kim→Thủy, Thủy→Mộc, Mộc→Hỏa, Hỏa→Thổ, Thổ→Kim.

### 2.3. Nội dung 3 dòng theo case

Helper `joinVi(list)`: nối bằng ", " và phần tử cuối bằng " và " ("Hỏa, Thổ và Kim").
Ký hiệu: X = hành thừa nặng nhất, Y = hành thiếu nặng nhất (hoặc hành Vua bị khắc ở case C),
S = hành X SINH ra (tiết khí), M = bản mệnh user.

**Case A — lệch chuẩn thông thường (thừa/thiếu nhưng không khắc trực tiếp):**
| Kind | Template |
|---|---|
| status | "Phòng đang có quá nhiều hành {joinVi(surpluses)}, nhưng lại thiếu hụt hành {joinVi(deficits)}." (vế nào rỗng bỏ vế đó; chỉ thiếu: "Phòng đang thiếu hụt hành {joinVi(deficits)}.") |
| detail | "{trait(X)} (hành {X}) đang quá trội, dìm {trait(Y)} của hành {Y} xuống mức suy hạn." — chỉ thiếu không thừa: "{trait(Y)} (hành {Y}) đang suy — phòng chưa đủ nguồn năng lượng này." |
| action | "Bổ sung ngay {items(Y)} (thuộc {Y}) để cân bằng lại luồng khí." |

**Case B — cân bằng ổn định (không tìm lỗi, chuyển sang củng cố):**
| Kind | Template |
|---|---|
| status | "Ngũ hành không gian hiện đang đạt trạng thái cân bằng, không có năng lượng nào bị lệch chuẩn." |
| detail | "Sự cân bằng này đang trợ lực rất tốt cho bản mệnh {napAmName(M)} của bạn, giúp duy trì {trait(M)}." — chưa có DOB: "Bố cục hiện tại đạt chuẩn lý tưởng cho mục đích {purposeVi}." |
| action | "Giữ nguyên bố cục hiện tại, hạn chế nhồi thêm {items(K)} (tính {K}) để tránh phá vỡ cấu trúc cân bằng." — K = hành có Current cao nhất |

**Case C — toxic (thừa cục bộ X và X khắc hành Vua / hành thiếu Y):**
| Kind | Template |
|---|---|
| status | "Xuất hiện năng lượng xung khắc trực tiếp: phòng đang dư thừa cục bộ hành {X}." |
| detail | "{trait(X)} (hành {X}) quá nhiều đang triệt tiêu {trait(Y)} của hành {Y} trong phòng." |
| action | "Đặt thêm {items(Y)} ({Y}) hoặc {items(S)} ({S}) để hút bớt tính {X} độc hại." |

Lưu ý ngôn từ: chẩn đoán (status/detail) chỉ nói theo **hành** — engine không track vật thể thật
trong phòng (input chỉ có Color/Material/Shape + hướng), nên không được viết "phòng nhét đầy
thiết bị điện tử". "Thiết bị điện tử, bể cá..." chỉ xuất hiện ở **action** với vai trò ví dụ vật phẩm.

### 2.4. Bảng ngữ nghĩa hành (static trong BE — `ElementSemantics.cs`)

**Trait + items mặc định (5 hành):**

| Hành | Trait mặc định                  | Items ví dụ (chỉ dùng ở action)                 |
| ---- | ------------------------------- | ----------------------------------------------- |
| Kim  | tính kỷ luật, sự sắc bén        | vật phẩm kim loại, chuông gió, thiết bị điện tử |
| Mộc  | sự sáng tạo, sinh trưởng        | cây xanh, đồ gỗ, tông xanh lá                   |
| Thủy | sự linh hoạt, thông suốt        | bể cá mini, gương kính, vật phẩm màu xanh dương |
| Hỏa  | nhiệt huyết, sức bật năng lượng | đèn ánh sáng ấm, nến, tông đỏ cam               |
| Thổ  | sự tĩnh lặng, vững chãi         | gốm sứ, đá tự nhiên, tông nâu vàng              |

**Override trait theo WorkPurpose** (enum thật: Office, Study, Creative, Reading, Gaming, Mixed, Other):

| Purpose | Hành | Trait override |
|---|---|---|
| Office | Kim | tính kỷ luật, hiệu suất làm việc |
| Office | Thổ | sự ổn định, đáng tin cậy |
| Study | Mộc | sự sáng tạo, khả năng học hỏi |
| Study | Thủy | dòng chảy tư duy, khả năng tiếp thu |
| Creative | Mộc | sức sáng tạo, ý tưởng nảy nở |
| Creative | Hỏa | cảm hứng, đam mê |
| Reading | Thổ | sự tĩnh lặng, tập trung sâu |
| Reading | Thủy | sự thông suốt, thấm hiểu |
| Gaming | Hỏa | phản xạ, sự hưng phấn |
| Gaming | Kim | sự sắc bén, quyết đoán |
| Mixed/Other | — | dùng trait mặc định |

Tra cứu: `(purpose, element)` override trước → fallback mặc định.

**Bảng KHẮC đầy đủ (5 cặp — dùng phân loại case C + câu detail):**

| X khắc Y | Diễn giải mẫu cho detail |
|---|---|
| Kim khắc Mộc | tính kỷ luật/máy móc triệt tiêu sự sáng tạo, sinh trưởng |
| Mộc khắc Thổ | sự phát triển ồ ạt làm xói mòn sự tĩnh lặng, vững chãi |
| Thổ khắc Thủy | sự trì trệ, nặng nề chặn dòng chảy linh hoạt của tư duy |
| Thủy khắc Hỏa | sự lạnh lẽo, ẩm thấp dập tắt nhiệt huyết, năng lượng |
| Hỏa khắc Kim | sự nóng nảy, bốc đồng nung chảy tính kỷ luật, sắc bén |

**Bảng SINH đầy đủ (5 cặp — dùng chọn hành tiết khí S ở action; X sinh S ⇒ S "hút bớt" X):**

| X sinh S | Gợi ý tiết khí khi X thừa |
|---|---|
| Kim sinh Thủy | thêm bể cá/gương (Thủy) để hút bớt Kim |
| Thủy sinh Mộc | thêm cây xanh (Mộc) để hút bớt Thủy |
| Mộc sinh Hỏa | thêm đèn ấm/nến (Hỏa) để hút bớt Mộc |
| Hỏa sinh Thổ | thêm gốm sứ/đá (Thổ) để hút bớt Hỏa |
| Thổ sinh Kim | thêm vật phẩm kim loại (Kim) để hút bớt Thổ |

Hai vòng này PHẢI dùng lại `FengShuiCalculator.Generates/Controls` có sẵn (đã verify khớp
chuẩn ngũ hành) — không định nghĩa lại dictionary mới, tránh lệch 2 nguồn.

**Bảng tên Nạp Âm (mới — cho câu detail case B):** engine hiện có `GetNapAmElement(birthYear)`
trả về HÀNH nhưng chưa có TÊN nạp âm ("Tuyền Trung Thủy"). Thêm `GetNapAmName(birthYear)` vào
`FengShuiCalculator`: bảng 30 nạp âm chuẩn của lục thập hoa giáp (2 năm liền kề / 1 nạp âm,
index = vị trí trong chu kỳ 60 năm tính từ Giáp Tý 1984). Không có DOB → dùng câu fallback.

Lý do đặt trong code (đã chốt): tri thức domain gần như bất biến, sửa qua git review + compiler
check, không tốn migration/seeder. Gom về 1 file duy nhất để sau này muốn chuyển seed DB thì dễ.

## 3. File cần tạo / sửa

**BE (`Features/Workspace/`):**
| File | Việc |
|---|---|
| `Engine/ElementSemantics.cs` (mới) | bảng trait/items + override theo purpose (tra cứu qua `FengShuiCalculator.Generates/Controls` có sẵn, không tự định nghĩa vòng mới) |
| `Engine/FengShuiCalculator.cs` | thêm `GetNapAmName(birthYear)` — bảng 30 tên nạp âm |
| `Engine/SpaceInsightBuilder.cs` (mới) | pure static: `(rows, purpose, customerElement?) → SpaceInsights` — phân loại A/B/C + build 3 dòng. Pure function → unit test dễ |
| `DTOs/WorkspaceIntakeDtos.cs` hoặc file DTO analysis | thêm `CompatibilityPercent`, `SpaceInsights`, `SpaceInsightLine` |
| `Services/WorkspaceProfileService.cs` | `GetElementAnalysisAsync`: tính compat% + gọi builder. Cần thêm: đọc bản mệnh user (`IUserRepository` qua UoW — đã có sẵn pattern trong recommendation) + purpose modifiers (đã load sẵn trong `AnalyzeAsync`) |

**FE (`features/recommendation/components/element-vector/` + users):**
| File | Việc |
|---|---|
| `SpaceInsightList.tsx` | bỏ toàn bộ logic suy diễn — render `insights.lines` (map Kind→icon: status=Check/AlertTriangle theo case, detail=Info, action=Target) |
| `types/workspace.d.ts` | thêm `compatibilityPercent`, `insights` vào ElementAnalysis type |
| `ProfileWorkspace.tsx` | `CompletenessRing` → đổi nguồn + label "% tương thích"; màu ring theo mức (đỏ <40, vàng <70, xanh ≥70) |

**Không đổi:** engine `WorkspaceElementAnalyzer`, `MissingFieldHints`, radar chart.

## 4. Edge cases

- Chưa chọn WorkspaceType/purpose → adjustedIdeal = ideal mặc định, insights vẫn chạy;
  trait dùng bảng mặc định (không override).
- Chưa có bản mệnh (user thiếu DOB) → Case B dùng câu fallback theo purpose (đã ghi ở 2.3).
- Nhiều cặp khắc cùng lúc → chọn cặp có |gap_X| + |gap_Y| lớn nhất, chỉ nói 1 cặp (tránh ngộp).
- Cả 5 hành đều lệch nhẹ dưới STRONG → Case A bình thường, không bao giờ rơi vào C oan.
- Compat% và insights phải nhất quán: Case B ⇔ compat% cao — vì cùng nguồn Gap nên tự nhiên đúng,
  thêm unit test khẳng định (B thì compat ≥ 85 với ε=0.05).

## 5. Test checklist

- [ ] Unit `SpaceInsightBuilder`: 3 case A/B/C với vector tự chế; joinVi 1/2/3 phần tử ("và" đúng chỗ)
- [ ] Case C đúng cặp khắc (Kim thừa mạnh + Mộc thiếu → toxic; Kim thừa + Hỏa thiếu → A vì Kim không khắc Hỏa)
- [ ] Tiết khí đúng vòng sinh (Kim thừa → gợi ý Thủy)
- [ ] Override trait theo purpose (Study + Mộc ra "khả năng học hỏi")
- [ ] Compat%: phòng cân bằng hoàn hảo = 100, lệch cực đại = 0
- [ ] FE render đúng icon theo Kind + ring đổi màu theo mức
- [ ] Workspace thiếu purpose/bản mệnh không crash, ra câu fallback
- [ ] `GetNapAmName`: 1984→"Hải Trung Kim", 1991→"Lộ Bàng Thổ", 1997→"Giản Hạ Thủy" (đối chiếu bảng lục thập hoa giáp)
- [ ] Đủ 5 cặp khắc + 5 cặp sinh đều ra đúng câu (loop test qua cả 2 bảng)
