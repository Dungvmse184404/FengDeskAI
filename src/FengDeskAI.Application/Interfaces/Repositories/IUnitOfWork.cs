namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IUnitOfWork
{
    IUserRepository Users { get; }
    IAuthorizationAuditRepository AuthorizationAudits { get; }
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
    IReturnRepository Returns { get; }

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
    /// <summary>Sổ cái tiền nhà vườn + sàn — chỉ ghi qua <c>ILedgerService</c>.</summary>
    ILedgerRepository Ledger { get; }
    /// <summary>Lịch sử phí sàn — chỉ ghi qua <c>IPlatformFeeService</c>.</summary>
    IPlatformFeeRateRepository PlatformFeeRates { get; }

    // Promotion
    IVoucherRepository Vouchers { get; }

    // Notification
    INotificationRepository Notifications { get; }

    // Chat
    IChatboxRepository Chatboxes { get; }
    IChatMessageRepository ChatMessages { get; }

    /// <summary>Draft đơn hàng trợ lý AI (tối đa 1 draft Pending mỗi phòng chat riêng).</summary>
    IAiOrderDraftRepository AiOrderDrafts { get; }

    // CustomerCare
    IReviewRepository Reviews { get; }

    // Recommendation (AI)
    IRecommendationRepository Recommendations { get; }

    /// <summary>Cấu hình engine chấm điểm v3 (params, map ngũ hành, vector loại phòng...).</summary>
    IScoringConfigRepository ScoringConfig { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default);

    Task ReloadEntityAsync<TEntity>(TEntity entity) where TEntity : class;
}
