using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Infrastructure.Persistence.Contexts;

namespace FengDeskAI.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(
        AppDbContext context,
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IWorkspaceProfileRepository workspaceProfiles,
        ILocationRepository locations,
        IUserAddressRepository userAddresses,
        IStoreRepository stores,
        ICategoryRepository categories,
        ITagRepository tags,
        IProductRepository products,
        ICartRepository carts,
        IOrderRepository orders,
        IShippingRepository shipping,
        ITransactionRepository transactions)
    {
        _context = context;
        Users = users;
        RefreshTokens = refreshTokens;
        WorkspaceProfiles = workspaceProfiles;
        Locations = locations;
        UserAddresses = userAddresses;
        Stores = stores;
        Categories = categories;
        Tags = tags;
        Products = products;
        Carts = carts;
        Orders = orders;
        Shipping = shipping;
        Transactions = transactions;
    }

    public IUserRepository Users { get; }
    public IRefreshTokenRepository RefreshTokens { get; }
    public IWorkspaceProfileRepository WorkspaceProfiles { get; }
    public ILocationRepository Locations { get; }
    public IUserAddressRepository UserAddresses { get; }
    public IStoreRepository Stores { get; }
    public ICategoryRepository Categories { get; }
    public ITagRepository Tags { get; }
    public IProductRepository Products { get; }
    public ICartRepository Carts { get; }
    public IOrderRepository Orders { get; }
    public IShippingRepository Shipping { get; }
    public ITransactionRepository Transactions { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await action(ct);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
