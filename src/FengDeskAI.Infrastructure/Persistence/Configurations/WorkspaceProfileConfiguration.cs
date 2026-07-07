using System.Text.Json;
using System.Text.Json.Serialization;
using FengDeskAI.Domain.Entities.Workspace;
using FengDeskAI.Domain.Enums.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class WorkspaceProfileConfiguration : IEntityTypeConfiguration<WorkspaceProfile>
{
    public void Configure(EntityTypeBuilder<WorkspaceProfile> builder)
    {
        builder.ToTable("workspace_profiles");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasColumnName("id");
        builder.Property(w => w.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(w => w.WorkspaceTypeId).HasColumnName("workspace_type_id");

        builder.Property(w => w.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        // Lưu enum dưới dạng string trong DB cho dễ đọc + tránh phải migration khi đổi enum order
        builder.Property(w => w.LocationType).HasColumnName("location_type").HasConversion<string>().HasMaxLength(30);
        builder.Property(w => w.StyleCode).HasColumnName("style_code").HasMaxLength(30).IsRequired();
        builder.Property(w => w.Lighting).HasColumnName("lighting").HasConversion<string>().HasMaxLength(30);
        builder.Property(w => w.DeskType).HasColumnName("desk_type").HasConversion<string>().HasMaxLength(30);
        builder.Property(w => w.DeskOrientation).HasColumnName("desk_orientation").HasConversion<string>().HasMaxLength(15);
        builder.Property(w => w.RoomFacingDirection).HasColumnName("room_facing_direction").HasConversion<string>().HasMaxLength(15);
        builder.Property(w => w.WorkPurpose).HasColumnName("work_purpose").HasConversion<string>().HasMaxLength(30);
        builder.Property(w => w.FengShuiElement).HasColumnName("feng_shui_element").HasConversion<string>().HasMaxLength(10);

        // Directional Validation (engine v3) — nullable hướng + list hướng tối lưu jsonb.
        builder.Property(w => w.EntranceDirection).HasColumnName("entrance_direction").HasConversion<string>().HasMaxLength(15);
        builder.Property(w => w.ToiletDirection).HasColumnName("toilet_direction").HasConversion<string>().HasMaxLength(15);

        var jsonOpts = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        builder.Property(w => w.DarkDirections)
            .HasColumnName("dark_directions")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, jsonOpts),
                v => string.IsNullOrEmpty(v)
                    ? new List<CompassDirection>()
                    : JsonSerializer.Deserialize<List<CompassDirection>>(v, jsonOpts) ?? new List<CompassDirection>(),
                new ValueComparer<List<CompassDirection>>(
                    (a, b) => a!.SequenceEqual(b!),
                    c => c.Aggregate(0, (h, e) => HashCode.Combine(h, e.GetHashCode())),
                    c => c.ToList()));

        builder.Property(w => w.DeskArea).HasColumnName("desk_area");
        builder.Property(w => w.IsDefault).HasColumnName("is_default").HasDefaultValue(false);

        builder.Property(w => w.CreatedAt).HasColumnName("created_at");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");
        builder.Property(w => w.CreatedBy).HasColumnName("created_by");
        builder.Property(w => w.UpdatedBy).HasColumnName("updated_by");
        builder.Property(w => w.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(w => w.UserId);
        builder.HasIndex(w => w.StyleCode);

        // FK tới bảng tra cứu styles (Restrict).
        builder.HasOne<FengDeskAI.Domain.Entities.Catalog.Style>()
            .WithMany()
            .HasForeignKey(w => w.StyleCode)
            .OnDelete(DeleteBehavior.Restrict);

        // Mỗi user chỉ có 1 default profile — partial unique index (PG-specific)
        builder.HasIndex(w => w.UserId)
            .HasDatabaseName("UX_workspace_profiles_user_default")
            .IsUnique()
            .HasFilter("is_default = TRUE AND is_deleted = FALSE");

        builder.HasOne(w => w.User)
            .WithMany(u => u.WorkspaceProfiles)
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Xóa loại không gian → profile về null (coi như riêng tư, weight 1.0).
        builder.HasIndex(w => w.WorkspaceTypeId);
        builder.HasOne(w => w.WorkspaceType)
            .WithMany()
            .HasForeignKey(w => w.WorkspaceTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(w => !w.IsDeleted);
    }
}
