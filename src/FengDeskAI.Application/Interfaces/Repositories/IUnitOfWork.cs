namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IUnitOfWork
{
    IUserRepository Users { get; }
    IRefreshTokenRepository RefreshTokens { get; }
    IWorkspaceProfileRepository WorkspaceProfiles { get; }
    IWorkspaceTypeRepository WorkspaceTypes { get; }

    // Geography
    ILocationRepository Locations { get; }
    IUserAddressRepository UserAddresses { get; }

    // Vendor
    IStoreRepository Stores { get; }

    // Sales
    ICartRepository Carts { get; }
    IOrderRepository Orders { get; }

    // Catalog
    ICategoryRepository Categories { get; }
    ITagRepository Tags { get; }
    IProductRepository Products { get; }
    IGenericRepository<Domain.Entities.Catalog.Style> Styles { get; }
    IGenericRepository<Domain.Entities.Catalog.Vibe> Vibes { get; }
    IGenericRepository<Domain.Entities.Catalog.Element> Elements { get; }

    // Shipping
    IShippingRepository Shipping { get; }

    // Payment
    ITransactionRepository Transactions { get; }

    // Notification
    INotificationRepository Notifications { get; }

    // Chat
    IChatboxRepository Chatboxes { get; }
    IChatMessageRepository ChatMessages { get; }

    // CustomerCare
    IReviewRepository Reviews { get; }

    // Recommendation (AI)
    IRecommendationRepository Recommendations { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default);

    Task ReloadEntityAsync<TEntity>(TEntity entity) where TEntity : class;
}
