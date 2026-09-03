using FengDeskAI.Application.Common.Results;
using FengDeskAI.Domain.Common;
using FengDeskAI.WebAPI.Common.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// Lưới an toàn đổi exception do dữ liệu người dùng gửi thành 4xx/409 thay vì 500 trần.
///
/// Vì sao phải test riêng ở tầng unit: đường đi qua HTTP chỉ chứng minh được vài mã lỗi Postgres mà
/// ta dựng nổi tình huống (chuỗi quá dài, số vượt miền). Những mã còn lại — vi phạm khóa ngoại, khóa
/// trùng, ràng buộc CHECK — rất khó ép ra từ ngoài, mà chúng lại là phần dễ phân loại sai nhất.
///
/// Ràng buộc quan trọng nhất được khẳng định ở đây là chiều NGƯỢC LẠI: exception KHÔNG thuộc nhóm
/// "lỗi do người gọi" phải được để nguyên cho thoát ra 500. Một filter bắt quá tay sẽ biến lỗi
/// server thật thành 400 và giấu luôn sự cố — tệ hơn nhiều so với việc không có filter.
/// </summary>
public class ApiExceptionFilterTests
{
    [Fact(DisplayName = "FILTER-01 [Normal] An invalid state transition becomes 409")]
    public void InvalidStateTransition_BecomesConflict()
    {
        var context = Run(new InvalidStateTransitionException("ReturnRequest", "Requested", "VendorResponse"));

        var result = AssertHandled(context, expectedStatus: 409);
        Assert.Contains("Requested", result.Message!);
    }

    [Fact(DisplayName = "FILTER-02 [Normal] A numeric overflow becomes 400")]
    public void OverflowException_BecomesBadRequest()
    {
        var context = Run(new OverflowException("Arithmetic operation resulted in an overflow."));

        AssertHandled(context, expectedStatus: 400);
    }

    [Theory(DisplayName = "FILTER-03 [Normal] Postgres data errors map to the right client status")]
    [InlineData("22001", 400)] // chuỗi dài hơn cột
    [InlineData("22003", 400)] // số vượt miền numeric
    [InlineData("23503", 400)] // khóa ngoại không tồn tại
    [InlineData("23502", 400)] // thiếu cột NOT NULL
    [InlineData("23514", 400)] // vi phạm ràng buộc CHECK
    [InlineData("23505", 409)] // trùng khóa duy nhất
    public void PostgresDataError_MapsToClientStatus(string sqlState, int expectedStatus)
    {
        var context = Run(new DbUpdateException("save failed", new Npgsql.PostgresException(
            messageText: "test", severity: "ERROR", invariantSeverity: "ERROR", sqlState: sqlState)));

        var result = AssertHandled(context, expectedStatus);
        Assert.False(string.IsNullOrWhiteSpace(result.Message),
            "Client phải nhận được thông điệp đọc được, không phải phong bì rỗng.");
    }

    [Fact(DisplayName = "FILTER-04 [Boundary] An infrastructure database error is left to surface as 500")]
    public void PostgresInfrastructureError_IsNotHandled()
    {
        // 40P01 deadlock_detected — lỗi hạ tầng, không phải lỗi của người gọi. Đổi nó thành 400 sẽ
        // khiến client tưởng dữ liệu mình sai và không bao giờ thử lại, còn sự cố thật thì im lặng.
        var context = Run(new DbUpdateException("save failed", new Npgsql.PostgresException(
            messageText: "deadlock", severity: "ERROR", invariantSeverity: "ERROR", sqlState: "40P01")));

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    [Fact(DisplayName = "FILTER-05 [Boundary] A DbUpdateException without a Postgres cause is left alone")]
    public void DbUpdateExceptionWithoutPostgresCause_IsNotHandled()
    {
        var context = Run(new DbUpdateException("concurrency", new InvalidOperationException("nội bộ")));

        Assert.False(context.ExceptionHandled);
    }

    [Fact(DisplayName = "FILTER-06 [Boundary] An ordinary server exception is left to surface as 500")]
    public void UnrelatedException_IsNotHandled()
    {
        var context = Run(new NullReferenceException("lỗi lập trình thật"));

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    [Fact(DisplayName = "FILTER-07 [Boundary] The 401 filter keeps ownership of missing-claim errors")]
    public void UnauthorizedAccessException_IsNotHandled()
    {
        // UnauthorizedExceptionFilter chạy trước và xử lý ca này. Nếu filter mới cũng bắt thì hai
        // filter tranh nhau và mã trả về phụ thuộc thứ tự đăng ký — rất dễ vỡ khi ai đó sắp lại.
        var context = Run(new UnauthorizedAccessException("Token không hợp lệ."));

        Assert.False(context.ExceptionHandled);
    }

    // ---------------- Helper ----------------

    private static ExceptionContext Run(Exception exception)
    {
        var context = new ExceptionContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>())
        {
            Exception = exception,
        };

        new ApiExceptionFilter(NullLogger<ApiExceptionFilter>.Instance).OnException(context);
        return context;
    }

    private static ServiceResult AssertHandled(ExceptionContext context, int expectedStatus)
    {
        Assert.True(context.ExceptionHandled, "Exception thuộc nhóm lỗi người gọi phải được filter xử lý.");

        var objectResult = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);

        var payload = Assert.IsAssignableFrom<ServiceResult>(objectResult.Value);
        Assert.False(payload.IsSuccess);
        Assert.Equal(expectedStatus, payload.StatusCode);
        return payload;
    }
}
