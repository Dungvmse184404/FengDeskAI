using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FengDeskAI.WebAPI.Common.Filters;

/// <summary>
/// Lưới an toàn cuối cùng: đổi những exception có NGUYÊN NHÂN LÀ DỮ LIỆU NGƯỜI DÙNG GỬI thành
/// 4xx/409 kèm phong bì <see cref="ServiceResult"/> chuẩn, thay vì để chúng thoát ra thành 500 trần.
///
/// Vì sao cần: request gửi chuỗi dài quá cột, số vượt miền <c>numeric</c>, hay khóa ngoại không tồn
/// tại đều là **lỗi của người gọi**, nhưng nếu service quên chặn thì Postgres mới là nơi phát hiện —
/// lúc đó đã ở trong <c>SaveChangesAsync</c> và ném <see cref="DbUpdateException"/>. Trả 500 cho
/// những ca này vừa sai ngữ nghĩa (client tưởng server hỏng nên retry), vừa giấu mất lỗi thật.
///
/// Đây KHÔNG thay cho việc validate ở service. Service vẫn nên chặn sớm để trả thông điệp tiếng Việt
/// đúng chỗ sai ("Tên cửa hàng tối đa 255 ký tự") — filter này chỉ đảm bảo chỗ nào lọt thì vẫn ra
/// 4xx chứ không ra 500. Thứ tự chạy: filter này đặt SAU <see cref="UnauthorizedExceptionFilter"/>
/// nên 401 vẫn do filter kia xử lý trước.
///
/// Exception KHÔNG thuộc các nhóm dưới đây vẫn để thoát ra 500 — đó mới đúng là lỗi server và không
/// được che giấu.
/// </summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger) => _logger = logger;

    public void OnException(ExceptionContext context)
    {
        var (statusCode, message) = Classify(context.Exception);
        if (statusCode is null) return;

        // Vẫn log nguyên exception: trả 4xx cho client không có nghĩa là bỏ qua. Đây là chỗ service
        // quên validate, và log là thứ duy nhất chỉ ra chỗ đó.
        _logger.LogWarning(context.Exception,
            "Request bị từ chối ở tầng lưới an toàn ({StatusCode}) — service nên validate sớm hơn: {Path}",
            statusCode, context.HttpContext.Request.Path);

        context.Result = new ObjectResult(ServiceResult.Failure(statusCode.Value, message!))
        {
            StatusCode = statusCode.Value,
        };
        context.ExceptionHandled = true;
    }

    private static (int? StatusCode, string? Message) Classify(Exception exception) => exception switch
    {
        // Chuyển trạng thái không hợp lệ theo máy trạng thái nghiệp vụ → 409, đúng như tài liệu
        // của các controller RMA mô tả.
        InvalidStateTransitionException ex => (ApiStatusCodes.Conflict, ex.Message),

        // Số vượt miền (int tràn khi cộng dồn / nhân), decimal tràn khi cộng vector.
        OverflowException => (ApiStatusCodes.BadRequest,
            "Giá trị số vượt quá phạm vi cho phép. Vui lòng kiểm tra lại số lượng và các giá trị đã nhập."),

        DbUpdateException ex => ClassifyDatabase(ex),

        _ => (null, null),
    };

    /// <summary>
    /// Phân loại theo SQLSTATE của Postgres. Chỉ nhận diện những mã mà nguyên nhân chắc chắn là dữ
    /// liệu đầu vào; mã khác (deadlock, mất kết nối…) vẫn để thành 500 vì đó là lỗi hạ tầng thật.
    /// </summary>
    private static (int? StatusCode, string? Message) ClassifyDatabase(DbUpdateException exception)
        => exception.InnerException is not PostgresException pg
            ? (null, null)
            : pg.SqlState switch
            {
                // 22001 string_data_right_truncation — chuỗi dài hơn độ dài cột.
                PostgresErrorCodes.StringDataRightTruncation => (ApiStatusCodes.BadRequest,
                    "Một trong các trường nhập vào dài hơn giới hạn cho phép. Vui lòng rút ngắn rồi thử lại."),

                // 22003 numeric_value_out_of_range — số vượt precision/scale của cột.
                PostgresErrorCodes.NumericValueOutOfRange => (ApiStatusCodes.BadRequest,
                    "Một trong các giá trị số nằm ngoài phạm vi cho phép. Vui lòng kiểm tra lại."),

                // 23503 foreign_key_violation — tham chiếu tới bản ghi không tồn tại.
                PostgresErrorCodes.ForeignKeyViolation => (ApiStatusCodes.BadRequest,
                    "Dữ liệu tham chiếu tới một bản ghi không tồn tại. Vui lòng kiểm tra lại các mã đã chọn."),

                // 23505 unique_violation — trùng khóa duy nhất.
                PostgresErrorCodes.UniqueViolation => (ApiStatusCodes.Conflict,
                    "Dữ liệu đã tồn tại. Vui lòng dùng giá trị khác."),

                // 23502 not_null_violation — thiếu trường bắt buộc.
                PostgresErrorCodes.NotNullViolation => (ApiStatusCodes.BadRequest,
                    "Thiếu một trường bắt buộc. Vui lòng kiểm tra lại dữ liệu gửi lên."),

                // 23514 check_violation — vi phạm ràng buộc CHECK của bảng.
                PostgresErrorCodes.CheckViolation => (ApiStatusCodes.BadRequest,
                    "Dữ liệu không thỏa ràng buộc của hệ thống. Vui lòng kiểm tra lại."),

                _ => (null, null),
            };
}
