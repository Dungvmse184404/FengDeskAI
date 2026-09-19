using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FengDeskAI.ApiTests.Infrastructure;

/// <summary>
/// Thao tác "tua đồng hồ" cho công nợ nhà cung cấp.
///
/// Vì sao phải sửa thẳng DB: hạn phản đối luôn được đặt là <c>UtcNow + 7 ngày</c> lúc tạo, không có
/// endpoint nào đổi nó. Muốn kiểm nhánh "quá hạn" thì chỉ còn cách kéo hạn về quá khứ.
/// May là <c>DisputeDeadline</c> còn setter public nên vẫn đi qua EF được, không phải viết SQL thô.
/// </summary>
public static class VendorLiabilityMaintenance
{
    public static async Task BackdateDeadlineAsync(ApiTestFixture fixture, Guid liabilityId, DateTime deadlineUtc)
    {
        await fixture.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var liability = await db.Set<VendorLiability>().FirstAsync(v => v.Id == liabilityId);
            liability.DisputeDeadline = deadlineUtc;
            await db.SaveChangesAsync();
        });
    }
}
