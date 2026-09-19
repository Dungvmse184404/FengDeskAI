using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash");

        builder.Property(u => u.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(255)
            .IsRequired();

        // Cột là `timestamp with time zone`, còn giá trị đi vào từ JSON ("1988-05-12") được
        // System.Text.Json dựng với Kind=Unspecified → Npgsql NÉM:
        //   "Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with time zone'".
        // Chuẩn hoá ở đây thay vì ở từng call site (đăng ký, finalize đăng ký, cập nhật hồ sơ) — thêm
        // đường ghi mới là quên mất một chỗ.
        //
        // ⚠️ Lấy `.Date` TRƯỚC khi đóng dấu UTC, KHÔNG quy đổi múi giờ. Ngày sinh là một ngày trên lịch,
        // không phải một mốc thời gian: "1988-01-01T00:00+07:00" mà đem đổi sang UTC thành
        // 1987-12-31T17:00Z ⇒ lùi một ngày ⇒ `GetLunarYear` ra NĂM ÂM khác ⇒ SAI MỆNH. Đúng loại bug mà
        // CLAUDE.md đã cảnh báo cho 5 call site trước đây.
        builder.Property(u => u.DateOfBirth)
            .HasColumnName("date_of_birth")
            .HasConversion(new ValueConverter<DateTime, DateTime>(
                toDb => DateTime.SpecifyKind(toDb.Date, DateTimeKind.Utc),
                fromDb => DateTime.SpecifyKind(fromDb, DateTimeKind.Utc)));
        builder.Property(u => u.BirthTime).HasColumnName("birth_time");
        builder.Property(u => u.Gender).HasColumnName("gender").HasConversion<int>();
        builder.Property(u => u.Phone).HasColumnName("phone").HasMaxLength(20);
        builder.HasIndex(u => u.Phone).IsUnique().HasFilter("phone IS NOT NULL");

        builder.Property(u => u.Role).HasColumnName("role").HasConversion<int>();
        builder.Property(u => u.Balance).HasColumnName("balance").HasPrecision(12, 3);
        builder.Property(u => u.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(u => u.TokenVersion).HasColumnName("token_version").HasDefaultValue(0);

        builder.Property(u => u.AuthProvider).HasColumnName("auth_provider").HasConversion<int>().HasDefaultValue(AuthProvider.Local);
        builder.Property(u => u.GoogleId).HasColumnName("google_id").HasMaxLength(255);
        builder.HasIndex(u => u.GoogleId).IsUnique().HasFilter("google_id IS NOT NULL");

        builder.Property(u => u.OccupationId).HasColumnName("occupation_id");
        // Restrict chứ không Cascade: xóa một nghề không được phép kéo theo tài khoản người dùng nào.
        builder.HasOne(u => u.Occupation)
            .WithMany()
            .HasForeignKey(u => u.OccupationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.CreatedBy).HasColumnName("created_by");
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by");
        builder.Property(u => u.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasQueryFilter(u => !u.IsDeleted);

        builder.HasMany(u => u.RefreshTokens)
            .WithOne(rt => rt.User)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
