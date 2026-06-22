using FengDeskAI.Application.Interfaces.Security;
using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Chat;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Entities.Announcement;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Entities.Shipping;
using FengDeskAI.Domain.Entities.Vendor;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Entities.Workspace;
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
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    public DbSet<ReturnItem> ReturnItems => Set<ReturnItem>();
    public DbSet<ReturnRequestImage> ReturnRequestImages => Set<ReturnRequestImage>();
    public DbSet<ReturnStatusLog> ReturnStatusLogs => Set<ReturnStatusLog>();

    // Shipping
    public DbSet<DeliveryProgressLog> DeliveryProgressLogs => Set<DeliveryProgressLog>();
    public DbSet<ShippingWebhook> ShippingWebhooks => Set<ShippingWebhook>();

    // Payment
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Refund> Refunds => Set<Refund>();

    // CustomerCare
    public DbSet<Review> Reviews => Set<Review>();

    // Notification
    public DbSet<Notification> Notifications => Set<Notification>();

    // Chat
    public DbSet<Chatbox> Chatboxes => Set<Chatbox>();
    public DbSet<ChatboxParticipant> ChatboxParticipants => Set<ChatboxParticipant>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatMessageImage> ChatMessageImages => Set<ChatMessageImage>();

    // Workspace
    public DbSet<WorkspaceProfile> WorkspaceProfiles => Set<WorkspaceProfile>();
    public DbSet<WorkspaceType> WorkspaceTypes => Set<WorkspaceType>();

    // Recommendation (AI)
    public DbSet<FengShuiRule> FengShuiRules => Set<FengShuiRule>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<RecommendationItem> RecommendationItems => Set<RecommendationItem>();
    public DbSet<RecommendationLog> RecommendationLogs => Set<RecommendationLog>();

    /// <summary>Map tới hàm Postgres <c>unaccent()</c> (extension unaccent) — chỉ dùng trong truy vấn EF
    /// để tìm kiếm không phân biệt dấu. Cần CREATE EXTENSION unaccent (đã bật trong migration).</summary>
    public static string Unaccent(string input) => throw new InvalidOperationException("Chỉ dùng trong truy vấn EF.");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.HasDbFunction(typeof(AppDbContext).GetMethod(nameof(Unaccent), new[] { typeof(string) })!)
            .HasName("unaccent");
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

        var orderStatusChanges = new List<OrderStatusLog>();

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

                    if (entry.Entity is Order order && entry.Property(nameof(Order.Status)).IsModified)
                    {
                        var oldStatus = entry.Property(nameof(Order.Status)).OriginalValue?.ToString();
                        var newStatus = entry.Property(nameof(Order.Status)).CurrentValue?.ToString();

                        if (oldStatus != newStatus)
                        {
                            orderStatusChanges.Add(new OrderStatusLog
                            {
                                OrderId = order.Id,
                                FromStatus = oldStatus ?? string.Empty,
                                ToStatus = newStatus ?? string.Empty,
                                ChangedAt = now,
                                ChangedBy = userId,
                                Note = order.StatusChangeNote
                            });
                        }
                    }

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

        if (orderStatusChanges.Any())
        {
            // Set audit cho log tự sinh (chúng được thêm SAU vòng lặp audit nên không tự có CreatedAt).
            foreach (var log in orderStatusChanges)
            {
                log.CreatedAt = now;
                log.UpdatedAt = now;
                if (userId.HasValue)
                {
                    log.CreatedBy = userId;
                    log.UpdatedBy = userId;
                }
            }
            this.AddRange(orderStatusChanges);
        }
    }
}
