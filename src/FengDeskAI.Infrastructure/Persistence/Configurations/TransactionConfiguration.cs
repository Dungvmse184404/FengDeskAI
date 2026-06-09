using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transaction");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(t => t.OrderCode).HasColumnName("order_code").IsRequired();
        builder.HasIndex(t => t.OrderCode).IsUnique();

        builder.Property(t => t.Amount).HasColumnName("amount").HasPrecision(12, 2);
        builder.Property(t => t.PaymentMethod).HasColumnName("payment_method").HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.ProviderTransactionId).HasColumnName("provider_transaction_id").HasMaxLength(100);
        builder.Property(t => t.PaidAt).HasColumnName("paid_at");

        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");
        builder.Property(t => t.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(t => t.OrderId);

        builder.HasOne(t => t.Order)
            .WithMany()
            .HasForeignKey(t => t.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
