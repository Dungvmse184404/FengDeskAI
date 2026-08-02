using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeModel3DQueueFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Luồng cũ tự động xử lý Initial bằng worker. Luồng mới đưa mọi request về cùng hàng chờ
            // staff; task Initial đã gửi Meshy được giữ lại dưới InProgress để staff preview/accept.
            migrationBuilder.Sql("""
                UPDATE model3d_requests
                SET status = CASE
                        WHEN status = 'Queued' THEN 'AwaitingStaff'
                        WHEN status = 'Processing' THEN 'InProgress'
                        ELSE status
                    END,
                    next_attempt_at = NULL,
                    updated_at = NOW()
                WHERE request_type = 'Initial'
                  AND status IN ('Queued', 'Processing')
                  AND NOT is_deleted;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE model3d_requests
                SET status = CASE
                        WHEN status = 'AwaitingStaff' THEN 'Queued'
                        WHEN status = 'InProgress' THEN 'Processing'
                        ELSE status
                    END,
                    updated_at = NOW()
                WHERE request_type = 'Initial'
                  AND status IN ('AwaitingStaff', 'InProgress')
                  AND NOT is_deleted;
                """);
        }
    }
}
