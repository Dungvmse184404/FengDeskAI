using FengDeskAI.Application.Interfaces.Security;
using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Entities.Shipping;
using FengDeskAI.Domain.Entities.Vendor;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Contexts;

public class AppDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Geography
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Ward> Wards => Set<Ward>();
    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();

    // Vendor
    public DbSet<GardenStore> GardenStores => Set<GardenStore>();
    public DbSet<StoreAddress> StoreAddresses => Set<StoreAddress>();
    public DbSet<GardenStaffAssignment> GardenStaffAssignments => Set<GardenStaffAssignment>();

    // Sales
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<OrderStatusLog> OrderStatusLogs => Set<OrderStatusLog>();

    // Shipping
    public DbSet<DeliveryProgressLog> DeliveryProgressLogs => Set<DeliveryProgressLog>();
    public DbSet<ShippingWebhook> ShippingWebhooks => Set<ShippingWebhook>();

    // Payment
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInformation();
        return base.SaveChanges();
    }

    private void ApplyAuditInformation()
    {
        var now = DateTime.UtcNow;
        var userId = _currentUser?.UserId;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    if (userId.HasValue)
                    {
                        entry.Entity.CreatedBy = userId;
                        entry.Entity.UpdatedBy = userId;
                    }
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    if (userId.HasValue) entry.Entity.UpdatedBy = userId;
                    entry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;
                    break;
                case EntityState.Deleted:
                    // Soft delete
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = now;
                    if (userId.HasValue) entry.Entity.UpdatedBy = userId;
                    break;
            }
        }
    }
}
