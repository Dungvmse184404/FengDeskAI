using FengDeskAI.Application.Features.Vendor.DTOs;
using FengDeskAI.Application.Features.Vendor.Services;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Entities.Vendor;
using FengDeskAI.Domain.Enums.Vendor;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class StoreRepository : GenericRepository<GardenStore>, IStoreRepository
{
    public StoreRepository(AppDbContext context) : base(context) { }

    public Task<List<GardenStore>> GetActiveAsync(CancellationToken ct = default)
        => _set.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(ct);

    public Task<GardenStore?> GetDetailAsync(Guid id, CancellationToken ct = default)
        => _set.Include(s => s.Address).ThenInclude(a => a!.Ward)
               .Include(s => s.Owners)
               .FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<List<GardenStore>> GetWithAddressByIdsAsync(IEnumerable<Guid> storeIds, CancellationToken ct = default)
        => _set.AsNoTracking()
               .Where(s => storeIds.Contains(s.Id))
               .Include(s => s.Address).ThenInclude(a => a!.Ward).ThenInclude(w => w.District).ThenInclude(d => d.Province)
               .ToListAsync(ct);

    public Task<List<GardenStore>> GetMissingCarrierShopIdAsync(int limit, CancellationToken ct = default)
        // Lọc ngay ở SQL mọi điều kiện kiểm được: chỉ store thực sự có thể đăng ký mới được nạp về.
        // Riêng SĐT phải chuẩn hoá (bỏ dấu cách/+84) nên không dịch sang SQL được — provisioner
        // lọc nốt trong bộ nhớ trước khi gọi nhà vận chuyển.
        => _set.AsNoTracking()
               .Where(s => s.IsActive
                        && s.GhnShopId == null
                        && s.Address != null
                        && s.Address.StreetAddress != ""
                        && s.Address.Ward.GhnWardCode != null
                        && s.Address.Ward.GhnWardCode != ""
                        && s.Address.Ward.District.GhnDistrictId != null)
               .Include(s => s.Address).ThenInclude(a => a!.Ward).ThenInclude(w => w.District)
               .OrderBy(s => s.CreatedAt)
               .Take(limit)
               .ToListAsync(ct);

    public async Task SetCarrierShopIdAsync(Guid storeId, int shopId, CancellationToken ct = default)
        => await _set.Where(s => s.Id == storeId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.GhnShopId, shopId)
                // ExecuteUpdate bỏ qua interceptor SaveChanges nên phải tự set UpdatedAt.
                .SetProperty(s => s.UpdatedAt, DateTime.UtcNow), ct);

    public async Task<bool> CanManageAsync(Guid storeId, Guid userId, CancellationToken ct = default)
    {
        if (await IsOwnerAsync(storeId, userId, ct)) return true;
        // Chỉ Accepted mới có quyền — Pending/Rejected/Revoked đều KHÔNG có quyền (đã thống nhất trong invitation flow).
        return await _context.Set<GardenStaffAssignment>()
            .AnyAsync(a => a.GardenStoreId == storeId && a.StaffId == userId && a.Status == InvitationStatus.Accepted, ct);
    }

    public Task<bool> IsOwnerAsync(Guid storeId, Guid userId, CancellationToken ct = default)
        => _context.Set<GardenStoreOwner>()
            .AnyAsync(o => o.GardenStoreId == storeId && o.OwnerUserId == userId, ct);

    public Task<bool> IsAcceptedStaffAsync(Guid storeId, Guid userId, CancellationToken ct = default)
        => _context.Set<GardenStaffAssignment>()
            .AnyAsync(a => a.GardenStoreId == storeId && a.StaffId == userId && a.Status == InvitationStatus.Accepted, ct);

    /// <summary>Bốn mốc thời gian mà biểu đồ doanh thu hỗ trợ — trả hết trong một lần gọi.</summary>
    private static readonly string[] StatsRanges = { "week", "month", "quarter", "year" };

    /// <summary>
    /// Bảng thống kê cửa hàng.
    ///
    /// <para>
    /// Hai điều chi phối cách viết hàm này:
    /// <list type="number">
    /// <item><b>Số lượt đi về DB mới là chi phí chính, không phải khối lượng dữ liệu.</b> DB nằm ở Sydney,
    /// mỗi round-trip ~300ms; bản cũ bắn 27 câu tuần tự ⇒ ~8s chỉ để chờ mạng. Nay còn 8 câu: mọi con số
    /// vô hướng được cộng trong C# từ vài danh sách cột mỏng kéo về một lần.</item>
    /// <item><b><paramref name="range"/> không đổi dữ liệu, chỉ đổi cách chia mốc.</b> Nên dựng luôn cả
    /// bốn chuỗi (<see cref="StoreStatisticsResponse.RevenueSeriesByRange"/>) — client đổi
    /// tuần/tháng/quý/năm là đổi tại chỗ, không gọi lại API. Chia mốc là vòng lặp trong bộ nhớ, rẻ hơn
    /// một round-trip rất nhiều.</item>
    /// </list>
    /// </para>
    /// </summary>
    public async Task<StoreStatisticsResponse> GetStatisticsAsync(Guid storeId, string? range = null, CancellationToken ct = default)
    {
        // [1/8] Mọi con số về delivery (đếm theo trạng thái, tiền đang xử lý, doanh thu, phí ship, đối
        // soát, ba lớp của biểu đồ) đều rút ra được từ 6 cột vô hướng này. Kéo một lần, cộng trong C#.
        var deliveryFacts = await _context.Set<Domain.Entities.Sales.Delivery>()
            .AsNoTracking()
            .Where(d => d.GardenStoreId == storeId)
            .Select(d => new
            {
                d.Status,
                d.Order.PaymentMethod,
                d.CreatedAt,
                d.DeliveredAt,
                d.Subtotal,
                d.ShippingFee,
            })
            .ToListAsync(ct);

        var byStatus = deliveryFacts
            .GroupBy(d => d.Status)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        // Đơn "đang xử lý": chưa tới tay khách — store còn việc phải làm, KHÔNG phụ thuộc đã thu tiền hay chưa.
        var active = deliveryFacts.Where(d => IsRunning(d.Status)).ToList();
        var activeValue = active.Sum(d => d.Subtotal);

        // Tiền đã THỰC SỰ vào hay chưa lại là chuyện khác: COD thu tại điểm giao, nên đơn COD đang trên
        // đường vẫn là tiền chưa có. Tách hai nhánh ở đây thay vì gộp hết vào "đã thanh toán".
        var activePaid = active.Where(d => d.PaymentMethod == Domain.Enums.Payment.PaymentMethod.PayOS).ToList();
        var activeCod = active.Where(d => d.PaymentMethod == Domain.Enums.Payment.PaymentMethod.COD).ToList();

        var delivered = deliveryFacts.Where(d => d.Status == Domain.Enums.Sales.DeliveryStatus.Delivered).ToList();
        var totalRevenue = delivered.Sum(d => d.Subtotal);
        var totalShippingFee = delivered.Sum(d => d.ShippingFee);

        // Tổng phí ship theo trạng thái — vẫn trả để FE hiện tổng, còn từng dòng sản phẩm có cột riêng
        // (phân bổ theo tỉ trọng tiền hàng, xem GroupItemsByProduct).
        var shippingByStatus = new Dictionary<string, decimal>
        {
            // Đơn PayOS chưa thanh toán chưa có delivery ⇒ chưa có phí ship; phần này là của COD đang giao.
            ["Ordered"] = activeCod.Sum(d => d.ShippingFee),
            ["Paid"] = activePaid.Sum(d => d.ShippingFee),
            ["Completed"] = totalShippingFee,
            ["Refunded"] = deliveryFacts
                .Where(d => d.Status == Domain.Enums.Sales.DeliveryStatus.Returned)
                .Sum(d => d.ShippingFee),
        };

        // Đối soát: tiền chỉ "sạch" sau khi giao xong và qua hết khoảng giữ (PayoutPolicy.HoldDays).
        // Mốc tính là DeliveredAt; đơn thiếu mốc đó (dữ liệu cũ) rơi về CreatedAt để không kẹt vĩnh viễn.
        var clearedBefore = DateTime.UtcNow.AddDays(-PayoutPolicy.HoldDays);
        var availableForPayout = delivered.Where(d => (d.DeliveredAt ?? d.CreatedAt) <= clearedBefore).Sum(d => d.Subtotal);
        var pendingClearance = delivered.Where(d => (d.DeliveredAt ?? d.CreatedAt) > clearedBefore).Sum(d => d.Subtotal);

        // [2/8] Món nào đang nằm trong đơn nào — gộp theo (SẢN PHẨM × TRẠNG THÁI) để một bảng kể được cả
        // vòng đời: đặt → trả tiền → giao xong → hoàn. Ba lớp gắn với delivery lấy chung MỘT câu rồi tách
        // trong bộ nhớ; trước đây là ba câu riêng dù điều kiện join y hệt nhau.
        var deliveryItems = await _context.Set<Domain.Entities.Sales.OrderItem>()
            .AsNoTracking()
            .Where(i => i.Delivery != null
                        && i.Delivery.GardenStoreId == storeId
                        && (i.Delivery.Status == Domain.Enums.Sales.DeliveryStatus.Pending
                            || i.Delivery.Status == Domain.Enums.Sales.DeliveryStatus.Confirmed
                            || i.Delivery.Status == Domain.Enums.Sales.DeliveryStatus.Preparing
                            || i.Delivery.Status == Domain.Enums.Sales.DeliveryStatus.Shipped
                            || i.Delivery.Status == Domain.Enums.Sales.DeliveryStatus.Delivered))
            .Select(i => new
            {
                i.ProductItem.ProductId,
                i.ProductItem.Product.Name,
                i.OrderId,
                Value = i.UnitPrice * i.Quantity,
                i.Quantity,
                DeliveryStatus = i.Delivery!.Status,
                i.Order.PaymentMethod,
                DeliveryFee = i.Delivery.ShippingFee,
                DeliverySubtotal = i.Delivery.Subtotal,
            })
            .ToListAsync(ct);

        var deliveryItemFacts = deliveryItems
            .Select(i => (i.DeliveryStatus, i.PaymentMethod, Fact: new ItemFact(
                i.ProductId, i.Name, i.OrderId, i.Value, i.Quantity, i.DeliveryFee, i.DeliverySubtotal)))
            .ToList();

        // COD đang giao = tiền CHƯA thu ⇒ cùng lớp với đơn online chưa trả, không phải lớp "đã thanh toán".
        var codRows = GroupItemsByProduct(deliveryItemFacts
            .Where(x => x.DeliveryStatus != Domain.Enums.Sales.DeliveryStatus.Delivered
                        && x.PaymentMethod == Domain.Enums.Payment.PaymentMethod.COD)
            .Select(x => x.Fact), "Ordered");
        var activeRows = GroupItemsByProduct(deliveryItemFacts
            .Where(x => x.DeliveryStatus != Domain.Enums.Sales.DeliveryStatus.Delivered
                        && x.PaymentMethod == Domain.Enums.Payment.PaymentMethod.PayOS)
            .Select(x => x.Fact), "Paid");
        var completedRows = GroupItemsByProduct(deliveryItemFacts
            .Where(x => x.DeliveryStatus == Domain.Enums.Sales.DeliveryStatus.Delivered)
            .Select(x => x.Fact), "Completed");

        // [3/8] Đơn online chưa thanh toán: Order.Pending + PayOS, chưa có delivery (delivery chỉ sinh khi
        // webhook báo tiền về). Expired/Cancelled không tính — đó là đơn đã chết. Lọc theo item thuộc vườn này.
        var awaitingItems = await _context.Set<Domain.Entities.Sales.OrderItem>()
            .AsNoTracking()
            .Where(i => i.Order.Status == Domain.Enums.Sales.OrderStatus.Pending
                        && i.Order.PaymentMethod == Domain.Enums.Payment.PaymentMethod.PayOS
                        && i.DeliveryId == null
                        && i.ProductItem.Product.GardenStoreId == storeId)
            .Select(i => new
            {
                i.ProductItem.ProductId,
                i.ProductItem.Product.Name,
                i.OrderId,
                Value = i.UnitPrice * i.Quantity,
                i.Quantity,
                OrderCreatedAt = i.Order.CreatedAt,
            })
            .ToListAsync(ct);

        var awaitingOrders = awaitingItems.Select(i => i.OrderId).Distinct().Count();
        var awaitingValue = awaitingItems.Sum(i => i.Value) + activeCod.Sum(d => d.Subtotal);
        // Chưa có delivery ⇒ chưa có phí ship để phân bổ, truyền 0.
        var awaitingRows = GroupItemsByProduct(awaitingItems
            .Select(i => new ItemFact(i.ProductId, i.Name, i.OrderId, i.Value, i.Quantity, 0m, 0m)), "Ordered");

        // [4/8] Hoàn tiền: đi qua ReturnItem (có sản phẩm + số lượng thật sự bị trả), chỉ tính ticket đã hoàn xong.
        var refundedRows = await _context.Set<Domain.Entities.Sales.ReturnItem>()
            .AsNoTracking()
            .Where(ri => ri.ReturnRequest.Delivery.GardenStoreId == storeId
                         && ri.ReturnRequest.Refund != null
                         && ri.ReturnRequest.Refund.Status == Domain.Enums.Payment.RefundStatus.Completed)
            .GroupBy(ri => new { ri.OrderItem.ProductItem.ProductId, ri.OrderItem.ProductItem.Product.Name })
            .Select(g => new StoreStatisticsItemRow
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.Name,
                Status = "Refunded",
                Quantity = g.Sum(x => x.Quantity),
                Value = g.Sum(x => x.UnitPrice * x.Quantity),
                OrderCount = g.Select(x => x.ReturnRequestId).Distinct().Count(),
            })
            .OrderByDescending(r => r.Value)
            .Take(10)
            .ToListAsync(ct);

        // [5/8] Lớp hoàn tiền của biểu đồ lấy từ Refund chứ không từ ReturnItem: số tiền hoàn thực tế có
        // thể lệch tổng tiền hàng bị trả (hoàn một phần, phí ship), và mốc là lúc hoàn XONG.
        var refundRows = await _context.Set<Domain.Entities.Payment.Refund>()
            .AsNoTracking()
            .Where(r => r.Status == Domain.Enums.Payment.RefundStatus.Completed
                        && r.ReturnRequest.Delivery.GardenStoreId == storeId)
            .Select(r => new { r.CompletedAt, r.CreatedAt, r.Amount })
            .ToListAsync(ct);

        // [6/8] Công nợ sẽ trừ vào kỳ chi kế tiếp; Waived đã được miễn nên không tính.
        var outstandingLiability = await _context.Set<Domain.Entities.Payment.VendorLiability>()
            .AsNoTracking()
            .Where(l => l.GardenStoreId == storeId
                        && l.Status != Domain.Enums.Payment.VendorLiabilityStatus.Waived)
            .SumAsync(l => (decimal?)l.Amount, ct) ?? 0m;

        // [7/8] và [8/8] — hai con số ở hai bảng khác, không gộp chung câu được.
        var productCount = await _context.Set<Domain.Entities.Catalog.Product>()
            .AsNoTracking()
            .CountAsync(p => p.GardenStoreId == storeId, ct);

        var staffCount = await _context.Set<GardenStaffAssignment>()
            .AsNoTracking()
            .CountAsync(a => a.GardenStoreId == storeId && a.Status == InvitationStatus.Accepted, ct);

        // ===================== Từ đây trở xuống không còn chạm DB =====================

        var now = DateTime.UtcNow;
        // Mỗi lớp của biểu đồ có mốc thời gian RIÊNG: đơn chưa trả tính theo lúc ĐẶT, đơn đang giao theo
        // lúc TẠO giao, đơn xong theo lúc GIAO XONG, hoàn tiền theo lúc HOÀN XONG.
        var completedSeries = delivered.Select(d => (At: d.DeliveredAt ?? d.CreatedAt, Amount: d.Subtotal)).ToList();
        var awaitingSeries = awaitingItems
            // Nhiều item cùng một đơn ⇒ gộp về đơn trước khi đếm, nếu không "3 đơn chờ trả" thành 3 dòng hàng.
            .GroupBy(x => x.OrderId)
            .Select(g => (At: g.First().OrderCreatedAt, Amount: g.Sum(x => x.Value)))
            // COD đang giao nằm cùng lớp: khách chưa trả đồng nào, tiền chỉ vào khi giao xong.
            .Concat(activeCod.Select(d => (At: d.CreatedAt, Amount: d.Subtotal)))
            .ToList();
        var inProgressSeries = activePaid.Select(d => (At: d.CreatedAt, Amount: d.Subtotal)).ToList();
        var refundSeries = refundRows.Select(r => (At: r.CompletedAt ?? r.CreatedAt, Amount: r.Amount)).ToList();

        // Dựng cả bốn mốc: chỉ là vòng lặp trên các danh sách đã có trong bộ nhớ.
        var seriesByRange = StatsRanges.ToDictionary(
            r => r,
            r => BuildRevenueSeries(r, now, completedSeries, awaitingSeries, inProgressSeries, refundSeries));

        // Doanh thu 6 tháng gần nhất — giữ cho client cũ.
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
        var revenueByMonth = completedSeries
            .Where(d => d.At >= monthStart)
            .GroupBy(d => new { d.At.Year, d.At.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyRevenuePoint
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Revenue = g.Sum(x => x.Amount),
                DeliveredCount = g.Count(),
            })
            .ToList();

        var normalized = NormalizeRange(range);
        return new StoreStatisticsResponse
        {
            TotalRevenue = totalRevenue,
            TotalShippingFee = totalShippingFee,
            TotalDeliveries = deliveryFacts.Count,
            DeliveriesByStatus = byStatus,
            ActiveDeliveries = active.Count,
            ActiveDeliveriesValue = activeValue,
            AwaitingPaymentOrders = awaitingOrders,
            AwaitingPaymentValue = awaitingValue,
            ItemsByStatus = awaitingRows.Concat(codRows).Concat(activeRows)
                .Concat(completedRows).Concat(refundedRows).ToList(),
            ShippingFeeByStatus = shippingByStatus,
            PayoutHoldDays = PayoutPolicy.HoldDays,
            AvailableForPayoutValue = availableForPayout,
            PendingClearanceValue = pendingClearance,
            OutstandingLiabilityValue = outstandingLiability,
            Range = normalized,
            RevenueSeries = seriesByRange[normalized],
            RevenueSeriesByRange = seriesByRange,
            ProductCount = productCount,
            StaffCount = staffCount,
            RevenueByMonth = revenueByMonth,
        };
    }

    /// <summary>Đơn chưa tới tay khách — store còn việc phải làm.</summary>
    private static bool IsRunning(Domain.Enums.Sales.DeliveryStatus status)
        => status is Domain.Enums.Sales.DeliveryStatus.Pending
            or Domain.Enums.Sales.DeliveryStatus.Confirmed
            or Domain.Enums.Sales.DeliveryStatus.Preparing
            or Domain.Enums.Sales.DeliveryStatus.Shipped;

    /// <summary>Một dòng hàng đã kéo về bộ nhớ — đủ để gộp theo sản phẩm và phân bổ phí ship của đơn.</summary>
    private sealed record ItemFact(
        Guid ProductId, string ProductName, Guid OrderId,
        decimal Value, int Quantity, decimal DeliveryFee, decimal DeliverySubtotal);

    /// <summary>
    /// Gộp order item theo sản phẩm: số lượng, tiền hàng, số đơn đang chứa. Lấy 10 dòng nặng tiền nhất —
    /// bảng trên dashboard chỉ để "biết đang tồn việc gì", muốn đủ thì vào trang đơn hàng.
    /// </summary>
    private static List<StoreStatisticsItemRow> GroupItemsByProduct(
        IEnumerable<ItemFact> items, string status)
    {
        // Phí ship thuộc về ĐƠN, không thuộc sản phẩm. Muốn có cột phí ship trên từng dòng thì phải phân
        // bổ, và cách duy nhất không thiên vị là theo TỈ TRỌNG TIỀN HÀNG của dòng trong đơn đó:
        //   ship(dòng) = Σ_đơn  phíShip(đơn) × tiềnDòng / tiềnHàng(đơn)
        // Cộng mọi dòng của một đơn lại đúng bằng phí ship của đơn — không phóng đại, không hụt.
        return items
            .GroupBy(r => new { r.ProductId, r.ProductName })
            .Select(g => new StoreStatisticsItemRow
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                Status = status,
                Quantity = g.Sum(x => x.Quantity),
                Value = g.Sum(x => x.Value),
                OrderCount = g.Select(x => x.OrderId).Distinct().Count(),
                ShippingFee = g.Sum(x =>
                    x.DeliverySubtotal > 0m ? x.DeliveryFee * x.Value / x.DeliverySubtotal : 0m),
            })
            .OrderByDescending(r => r.Value)
            .Take(10)
            .ToList();
    }

    /// <summary>Chỉ nhận 4 giá trị; rác hoặc bỏ trống → <c>month</c> (mặc định của dashboard).</summary>
    private static string NormalizeRange(string? range) => range?.Trim().ToLowerInvariant() switch
    {
        "week" => "week",
        "quarter" => "quarter",
        "year" => "year",
        _ => "month",
    };

    /// <summary>
    /// Chia doanh thu theo mốc của <paramref name="range"/>: tuần → 7 ngày, tháng → từng **nửa tuần**
    /// (khối 3-4 ngày, ~9 cột), quý → từng tuần (~13 cột), năm → 12 tháng.
    ///
    /// <para>
    /// Số cột nhắm vào khoảng 7-13. Tháng chia theo NGÀY cho 30 cột chen chúc không đọc được, chia theo
    /// TUẦN chỉ còn 4 cột thì chẳng thấy hình dạng gì — nửa tuần là chỗ đứng giữa hai cái sai đó.
    /// </para>
    ///
    /// <para>
    /// Mốc RỖNG vẫn được phát ra (revenue 0). Nếu chỉ trả mốc có dữ liệu thì biểu đồ co lại thành vài cột
    /// sát nhau và đọc như "tuần nào cũng bán được", che mất đúng thứ cần thấy là những ngày không có đơn.
    /// </para>
    /// </summary>
    private static List<RevenueBucket> BuildRevenueSeries(
        string range, DateTime now,
        IReadOnlyList<(DateTime At, decimal Amount)> completed,
        IReadOnlyList<(DateTime At, decimal Amount)> awaiting,
        IReadOnlyList<(DateTime At, decimal Amount)> inProgress,
        IReadOnlyList<(DateTime At, decimal Amount)> refunded)
    {
        var today = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var buckets = new List<(DateTime Start, DateTime End, string Label)>();

        switch (range)
        {
            case "week":
                for (int i = 6; i >= 0; i--)
                {
                    var d = today.AddDays(-i);
                    buckets.Add((d, d.AddDays(1), d.ToString("dd/MM")));
                }
                break;

            case "quarter":
            {
                // Đầu quý → từng tuần (mốc bắt đầu Thứ Hai), cắt ở hôm nay.
                int firstMonth = ((now.Month - 1) / 3) * 3 + 1;
                var quarterStart = new DateTime(now.Year, firstMonth, 1, 0, 0, 0, DateTimeKind.Utc);
                var cursor = StartOfWeek(quarterStart);
                while (cursor <= today)
                {
                    var end = cursor.AddDays(7);
                    // Tuần đầu bị cắt bởi mốc đầu quý — doanh thu trước đó thuộc quý trước, không gộp vào.
                    var start = cursor < quarterStart ? quarterStart : cursor;
                    buckets.Add((start, end, $"Tuần {start:dd/MM}"));
                    cursor = end;
                }
                break;
            }

            case "year":
                for (int m = 1; m <= 12; m++)
                {
                    var start = new DateTime(now.Year, m, 1, 0, 0, 0, DateTimeKind.Utc);
                    buckets.Add((start, start.AddMonths(1), $"Th {m:00}"));
                }
                break;

            default: // month — nửa tuần một cột (3-4 ngày), ~9 cột cho một tháng
            {
                var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                int days = DateTime.DaysInMonth(now.Year, now.Month);
                // Xen kẽ 4-3 ngày để 7 ngày khớp đúng một tuần, không trôi lệch dần qua các tuần.
                for (int i = 0; i < days;)
                {
                    int span = (i / 7) * 7 == i ? 4 : 3;
                    if (i + span > days) span = days - i;
                    var start = monthStart.AddDays(i);
                    var end = start.AddDays(span);
                    var lastDay = end.AddDays(-1);
                    buckets.Add((start, end, span == 1
                        ? start.ToString("dd/MM")
                        : $"{start:dd}-{lastDay:dd}/{start:MM}"));
                    i += span;
                }
                break;
            }
        }

        return buckets.Select(b =>
        {
            static (decimal Sum, int Count) Slice(
                IReadOnlyList<(DateTime At, decimal Amount)> rows, DateTime from, DateTime to)
            {
                decimal sum = 0m;
                int count = 0;
                foreach (var r in rows)
                    if (r.At >= from && r.At < to) { sum += r.Amount; count++; }
                return (sum, count);
            }

            var done = Slice(completed, b.Start, b.End);
            var wait = Slice(awaiting, b.Start, b.End);
            var doing = Slice(inProgress, b.Start, b.End);
            var back = Slice(refunded, b.Start, b.End);

            return new RevenueBucket
            {
                Start = b.Start,
                LabelVi = b.Label,
                Revenue = done.Sum,
                DeliveredCount = done.Count,
                Completed = done.Sum,
                CompletedCount = done.Count,
                AwaitingPayment = wait.Sum,
                AwaitingPaymentCount = wait.Count,
                InProgress = doing.Sum,
                InProgressCount = doing.Count,
                Refunded = back.Sum,
                RefundedCount = back.Count,
            };
        }).ToList();
    }

    /// <summary>Thứ Hai của tuần chứa <paramref name="date"/> — tuần ở VN bắt đầu từ Thứ Hai.</summary>
    private static DateTime StartOfWeek(DateTime date)
    {
        int diff = ((int)date.DayOfWeek + 6) % 7; // Chủ Nhật = 6, Thứ Hai = 0
        return date.AddDays(-diff);
    }

    public Task<List<Domain.Entities.Sales.Delivery>> GetDeliveriesToCreditAsync(
        DateTime maturedBefore, CancellationToken ct = default)
        => _context.Set<Domain.Entities.Sales.Delivery>()
            .Where(d => d.Status == Domain.Enums.Sales.DeliveryStatus.Delivered
                        && d.PayoutCreditedAt == null
                        && (d.DeliveredAt ?? d.CreatedAt) <= maturedBefore)
            // Chặn trên để một lượt quét không kéo cả nghìn dòng về; phần dư để chu kỳ sau xử lý tiếp.
            .OrderBy(d => d.DeliveredAt)
            .Take(200)
            .ToListAsync(ct);

    public Task<User?> GetPrimaryOwnerAsync(Guid storeId, CancellationToken ct = default)
        // GardenStoreOwner không có nav tới User nên join tay; entity trả về CÓ tracking để cộng số dư.
        => (from o in _context.Set<GardenStoreOwner>()
            where o.GardenStoreId == storeId && o.IsPrimary
            join u in _context.Set<User>() on o.OwnerUserId equals u.Id
            select u).FirstOrDefaultAsync(ct);

    public Task<List<GardenStore>> GetByOwnerAsync(Guid ownerUserId, CancellationToken ct = default)
        => _set.AsNoTracking()
            .Where(s => s.Owners.Any(o => o.OwnerUserId == ownerUserId))
            .Include(s => s.Address).ThenInclude(a => a!.Ward)
            .Include(s => s.Owners)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    public Task<List<GardenStore>> GetForUserAsync(Guid userId, CancellationToken ct = default)
        => _set.AsNoTracking()
            // Owner (mọi quan hệ sở hữu) HOẶC nhân viên đã Accepted — Pending/Rejected/Revoked KHÔNG được vào seller.
            .Where(s => s.Owners.Any(o => o.OwnerUserId == userId)
                || _context.Set<GardenStaffAssignment>().Any(a =>
                       a.GardenStoreId == s.Id
                       && a.StaffId == userId
                       && a.Status == InvitationStatus.Accepted))
            .Include(s => s.Address).ThenInclude(a => a!.Ward)
            .Include(s => s.Owners)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    public Task<List<GardenStoreOwner>> GetOwnersAsync(Guid storeId, CancellationToken ct = default)
        => _context.Set<GardenStoreOwner>().AsNoTracking()
            .Where(o => o.GardenStoreId == storeId)
            .OrderByDescending(o => o.IsPrimary).ThenBy(o => o.AssignedAt)
            .ToListAsync(ct);

    public Task<GardenStoreOwner?> GetOwnerAsync(Guid storeId, Guid userId, CancellationToken ct = default)
        => _context.Set<GardenStoreOwner>()
            .FirstOrDefaultAsync(o => o.GardenStoreId == storeId && o.OwnerUserId == userId, ct);

    public Task<int> CountOwnersAsync(Guid storeId, CancellationToken ct = default)
        => _context.Set<GardenStoreOwner>().CountAsync(o => o.GardenStoreId == storeId, ct);

    public async Task AddOwnerAsync(GardenStoreOwner owner, CancellationToken ct = default)
        => await _context.Set<GardenStoreOwner>().AddAsync(owner, ct);

    public Task<List<StaffAssignmentResponse>> GetStaffAsync(Guid storeId, CancellationToken ct = default)
    {
        // Trả về cả Pending + Accepted (để owner nhìn thấy lời mời chưa phản hồi).
        // Rejected/Revoked ẩn để giữ list gọn — owner muốn xem lịch sử có thể mở endpoint riêng.
        var users = _context.Set<User>().IgnoreQueryFilters();
        return _context.Set<GardenStaffAssignment>().AsNoTracking()
            .Where(a => a.GardenStoreId == storeId
                && (a.Status == InvitationStatus.Pending || a.Status == InvitationStatus.Accepted))
            .OrderByDescending(a => a.InvitedAt)
            .Select(a => new StaffAssignmentResponse
            {
                Id = a.Id,
                GardenStoreId = a.GardenStoreId,
                StaffId = a.StaffId,
                StaffName = users.Where(u => u.Id == a.StaffId).Select(u => u.FullName).FirstOrDefault() ?? string.Empty,
                StaffEmail = users.Where(u => u.Id == a.StaffId).Select(u => u.Email).FirstOrDefault() ?? string.Empty,
                StaffPhone = users.Where(u => u.Id == a.StaffId).Select(u => u.Phone).FirstOrDefault(),
                InvitedBy = a.InvitedBy,
                InvitedByName = users.Where(u => u.Id == a.InvitedBy).Select(u => u.FullName).FirstOrDefault(),
                Status = a.Status,
                InvitedAt = a.InvitedAt,
                RespondedAt = a.RespondedAt,
                UnassignedAt = a.UnassignedAt,
            })
            .ToListAsync(ct);
    }

    public Task<GardenStaffAssignment?> GetActiveAssignmentAsync(Guid storeId, Guid staffId, CancellationToken ct = default)
        => _context.Set<GardenStaffAssignment>()
            .FirstOrDefaultAsync(a => a.GardenStoreId == storeId && a.StaffId == staffId
                && (a.Status == InvitationStatus.Pending || a.Status == InvitationStatus.Accepted), ct);

    public Task<GardenStaffAssignment?> GetAssignmentByIdAsync(Guid assignmentId, Guid storeId, CancellationToken ct = default)
        => _context.Set<GardenStaffAssignment>()
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.GardenStoreId == storeId, ct);

    public Task<GardenStaffAssignment?> GetAssignmentByIdForUserAsync(Guid assignmentId, Guid staffUserId, CancellationToken ct = default)
        => _context.Set<GardenStaffAssignment>()
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.StaffId == staffUserId, ct);

    public Task<List<InvitationResponse>> GetPendingInvitationsForUserAsync(Guid staffUserId, CancellationToken ct = default)
    {
        var users = _context.Set<User>().IgnoreQueryFilters();
        var stores = _set.IgnoreQueryFilters();
        return _context.Set<GardenStaffAssignment>().AsNoTracking()
            .Where(a => a.StaffId == staffUserId && a.Status == InvitationStatus.Pending)
            .OrderByDescending(a => a.InvitedAt)
            .Select(a => new InvitationResponse
            {
                Id = a.Id,
                GardenStoreId = a.GardenStoreId,
                StoreName = stores.Where(s => s.Id == a.GardenStoreId).Select(s => s.Name).FirstOrDefault() ?? string.Empty,
                InvitedBy = a.InvitedBy,
                InvitedByName = users.Where(u => u.Id == a.InvitedBy).Select(u => u.FullName).FirstOrDefault(),
                Status = a.Status,
                InvitedAt = a.InvitedAt,
            })
            .ToListAsync(ct);
    }

    public async Task AddAssignmentAsync(GardenStaffAssignment assignment, CancellationToken ct = default)
        => await _context.Set<GardenStaffAssignment>().AddAsync(assignment, ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _set.IgnoreQueryFilters().AnyAsync(s => s.Id == id, ct);

    public Task<StoreAddress?> GetAddressAsync(Guid storeId, CancellationToken ct = default)
        => _context.Set<StoreAddress>().FirstOrDefaultAsync(a => a.StoreId == storeId, ct);

    public Task<StoreAddress?> GetAddressIncludingDeletedAsync(Guid storeId, CancellationToken ct = default)
        => _context.Set<StoreAddress>().IgnoreQueryFilters().FirstOrDefaultAsync(a => a.StoreId == storeId, ct);

    public Task<bool> AddressExistsAsync(Guid storeId, CancellationToken ct = default)
        => _context.Set<StoreAddress>().IgnoreQueryFilters().AnyAsync(a => a.StoreId == storeId, ct);

    public async Task AddAddressAsync(StoreAddress address, CancellationToken ct = default)
        => await _context.Set<StoreAddress>().AddAsync(address, ct);

    public async Task HardDeleteAsync(Guid id, CancellationToken ct = default)
    {
        // Bypass soft-delete interceptor (SaveChanges) bằng ExecuteDelete chạy SQL trực tiếp.
        await _context.Set<StoreAddress>().IgnoreQueryFilters()
            .Where(a => a.StoreId == id).ExecuteDeleteAsync(ct);
        await _context.Set<GardenStaffAssignment>().IgnoreQueryFilters()
            .Where(a => a.GardenStoreId == id).ExecuteDeleteAsync(ct);
        await _context.Set<GardenStoreOwner>().IgnoreQueryFilters()
            .Where(o => o.GardenStoreId == id).ExecuteDeleteAsync(ct);
        await _set.IgnoreQueryFilters()
            .Where(s => s.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task HardDeleteAddressAsync(Guid storeId, CancellationToken ct = default)
        => await _context.Set<StoreAddress>().IgnoreQueryFilters()
            .Where(a => a.StoreId == storeId).ExecuteDeleteAsync(ct);
}
