-- =====================================================================================
-- Bật Row Level Security cho các bảng trong schema `public`
--
-- CHẠY Ở ĐÂU: Supabase SQL Editor (hoặc psql với vai trò `postgres`).
-- AN TOÀN ĐỂ CHẠY LẠI: mọi câu đều idempotent, chạy bao nhiêu lần cũng ra cùng kết quả.
--
-- -------------------------------------------------------------------------------------
-- 1. Vì sao cần
--
-- Supabase phơi schema `public` ra PostgREST, và vai trò `anon` / `authenticated` được cấp
-- sẵn quyền trên mọi bảng ở đó. Kiểm tra ngày 25/09/2026 cho thấy 4 bảng dưới đây có:
--
--     anon: SELECT, INSERT, UPDATE, DELETE, TRUNCATE   +   RLS = OFF
--
-- Tức là bất kỳ ai cầm anon key (khoá này theo thiết kế là CÔNG KHAI, nằm trong client)
-- đều đọc, sửa và XOÁ TRẮNG được chúng qua PostgREST. `occupations`,
-- `occupation_element_profiles`, `product_aspirations` là dữ liệu nền của bộ chấm điểm
-- phong thuỷ — mất là hỏng toàn bộ gợi ý; `model3d_requests` là yêu cầu của người dùng.
--
-- -------------------------------------------------------------------------------------
-- 2. Vì sao BẬT RLS mà KHÔNG tạo policy nào
--
-- Đây đã là quy ước sẵn có của dự án: 61 bảng khác đang ở đúng trạng thái này. Lý do:
--
--   * API .NET kết nối bằng vai trò `postgres` — cũng chính là CHỦ SỞ HỮU mọi bảng. Chủ
--     sở hữu BỎ QUA RLS, trừ khi bật FORCE ROW LEVEL SECURITY (đang tắt, và phải giữ tắt).
--     ⇒ Bật RLS KHÔNG ảnh hưởng gì tới API. Đã kiểm chứng: 61 bảng kia vẫn chạy bình thường.
--   * Không có policy nào ⇒ `anon`/`authenticated` không khớp được dòng nào ⇒ chặn sạch.
--     Phân quyền thật nằm ở tầng .NET, không nhân đôi xuống DB.
--
-- ⚠ ĐỪNG "sửa" cảnh báo INFO "RLS Enabled No Policy" của Supabase bằng cách thêm policy.
--   Ở kiến trúc này, KHÔNG có policy mới là đúng. Thêm policy = mở cửa ra Internet.
-- ⚠ ĐỪNG bật FORCE ROW LEVEL SECURITY. Bật là API .NET mất quyền đọc toàn bộ DB.
-- =====================================================================================

BEGIN;

-- -------------------------------------------------------------------------------------
-- Phần A — 4 bảng đang thiếu (nêu tên rõ ràng để đọc diff biết chính xác đụng vào đâu)
-- -------------------------------------------------------------------------------------
ALTER TABLE public.model3d_requests            ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.occupations                 ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.occupation_element_profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.product_aspirations         ENABLE ROW LEVEL SECURITY;

-- -------------------------------------------------------------------------------------
-- Phần B — quét nốt bảng nào còn sót
--
-- Đây mới là phần chữa GỐC. 4 bảng trên lọt lưới vì chúng sinh ra từ EF migration, mà
-- migration thì không biết gì về RLS — nên cứ thêm bảng mới là lại thủng. Chạy lại khối
-- này sau mỗi đợt migration là xong.
--
-- Chỉ đụng bảng thường (relkind='r'): view và foreign table không có RLS.
-- -------------------------------------------------------------------------------------
DO $$
DECLARE
    t record;
    n int := 0;
BEGIN
    FOR t IN
        SELECT c.relname
        FROM pg_class c
        JOIN pg_namespace ns ON ns.oid = c.relnamespace
        WHERE ns.nspname = 'public'
          AND c.relkind = 'r'
          AND NOT c.relrowsecurity
        ORDER BY c.relname
    LOOP
        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', t.relname);
        RAISE NOTICE 'Đã bật RLS: public.%', t.relname;
        n := n + 1;
    END LOOP;

    IF n = 0 THEN
        RAISE NOTICE 'Không còn bảng nào thiếu RLS.';
    ELSE
        RAISE NOTICE 'Tổng cộng đã bật thêm % bảng.', n;
    END IF;
END $$;

COMMIT;

-- =====================================================================================
-- 3. Kiểm chứng — phải trả về 0 dòng
-- =====================================================================================
SELECT c.relname AS bang_con_thieu_rls
FROM pg_class c
JOIN pg_namespace ns ON ns.oid = c.relnamespace
WHERE ns.nspname = 'public' AND c.relkind = 'r' AND NOT c.relrowsecurity
ORDER BY 1;

-- Và phải chắc FORCE vẫn tắt trên mọi bảng (nếu có dòng trả về là API .NET sắp gãy):
SELECT c.relname AS bang_bi_force_rls
FROM pg_class c
JOIN pg_namespace ns ON ns.oid = c.relnamespace
WHERE ns.nspname = 'public' AND c.relkind = 'r' AND c.relforcerowsecurity
ORDER BY 1;

-- =====================================================================================
-- 4. (TUỲ CHỌN) Siết thêm một lớp nữa: thu hồi quyền của anon/authenticated
--
-- RLS đã đủ chặn. Phần này là "thắt lưng + dây đeo quần": cắt luôn quyền ở tầng GRANT nên
-- PostgREST trả 401/403 ngay, không cần tới RLS.
--
-- CHỈ chạy nếu KHÔNG có thứ gì đọc DB qua Supabase Data API. Đã kiểm tra ngày 25/09/2026:
-- FE chỉ dùng Supabase cho Storage (file .glb) — không có `supabase-js` truy vấn bảng, và
-- .env không có SUPABASE_URL/ANON_KEY. Nhưng nếu sau này ai đó thêm Edge Function hay
-- client dùng Data API thì khối này sẽ làm nó gãy — kiểm tra lại trước khi chạy.
--
-- KHÔNG thu hồi của `service_role`: vai trò này có BYPASSRLS, dành cho tác vụ máy chủ.
-- =====================================================================================
-- REVOKE ALL ON ALL TABLES IN SCHEMA public FROM anon, authenticated;
-- REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM anon, authenticated;
-- ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ALL ON TABLES FROM anon, authenticated;
-- ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ALL ON SEQUENCES FROM anon, authenticated;
