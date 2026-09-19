-- ============================================================================
-- Gỡ bản CŨ của migration 20260904030000_ElementInputApproval (cột is_approved)
-- để EF chạy lại bản MỚI (cột visibility 3 trạng thái).
--
-- VÌ SAO PHẢI LÀM TAY:
--   File migration đã bị sửa TẠI CHỖ (giữ nguyên MigrationId). EF luôn chạy Down()
--   từ CODE HIỆN TẠI — tức là đi DROP cột "visibility" chưa hề tồn tại trong DB.
--   => `dotnet ef database update 20260903040000_ElementInputLabelVi` sẽ LỖI.
--
-- CHẠY TRÊN: DB đã apply bản cũ (thường là dev localhost).
--   Máy chưa apply gì thì BỎ QUA file này, chạy thẳng `dotnet ef database update`.
--
-- SAU KHI CHẠY XONG: `dotnet ef database update`
-- ============================================================================

BEGIN;

-- 0) Kiểm tra: chỉ chạy khi DB đang ở đúng bản cũ.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'element_input_map' AND column_name = 'is_approved'
    ) THEN
        RAISE EXCEPTION 'DB không có cột is_approved — có thể bạn đã gỡ rồi, hoặc chưa từng apply bản cũ. Dừng lại.';
    END IF;
END $$;

-- 1) Trả element_input_map về trạng thái TRƯỚC migration.
DROP INDEX IF EXISTS "IX_element_input_map_is_approved_created_by";
ALTER TABLE element_input_map DROP COLUMN is_approved;

-- 2) Xóa dấu vết migration trong lịch sử để EF coi như chưa chạy.
DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20260904030000_ElementInputApproval';

COMMIT;

-- 3) Kiểm chứng — mong đợi: 0 dòng cả hai truy vấn.
SELECT column_name FROM information_schema.columns
WHERE table_name = 'element_input_map' AND column_name IN ('is_approved', 'visibility');

SELECT "MigrationId" FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20260904030000_ElementInputApproval';
