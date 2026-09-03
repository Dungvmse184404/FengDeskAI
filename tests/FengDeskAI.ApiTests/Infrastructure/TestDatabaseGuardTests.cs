using Xunit;

namespace FengDeskAI.ApiTests.Infrastructure;

/// <summary>
/// Chốt chặn DB là thứ đứng giữa bộ test và dữ liệu production — nên chính nó cũng phải có test.
/// Không dùng fixture: chạy độc lập, không cần API hay DB.
/// </summary>
public sealed class TestDatabaseGuardTests
{
    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=fengdeskai_test;Username=postgres;Password=postgres")]
    [InlineData("Host=127.0.0.1;Database=t;Username=u;Password=p")]
    [InlineData("Server=postgres;Database=t;Username=u;Password=p")]
    public void AllowsLocalPostgres(string connectionString)
        => TestDatabaseGuard.EnsureSafe(connectionString);

    [Fact]
    public void BlocksSupabaseProduction()
    {
        var supabase = "Host=aws-1-ap-southeast-2.pooler.supabase.com;Database=postgres;Username=x;Password=y";

        var ex = Assert.Throws<InvalidOperationException>(() => TestDatabaseGuard.EnsureSafe(supabase));
        Assert.Contains("supabase.com", ex.Message);
    }

    [Fact]
    public void BlocksAnyRemoteHost()
    {
        var remote = "Host=db.example.com;Database=postgres;Username=x;Password=y";

        var ex = Assert.Throws<InvalidOperationException>(() => TestDatabaseGuard.EnsureSafe(remote));
        Assert.Contains("db.example.com", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlocksMissingConnectionString(string? connectionString)
        => Assert.Throws<InvalidOperationException>(() => TestDatabaseGuard.EnsureSafe(connectionString));

    [Fact]
    public void BlocksUnreadableHost()
        => Assert.Throws<InvalidOperationException>(
            () => TestDatabaseGuard.EnsureSafe("Database=postgres;Username=x;Password=y"));
}
