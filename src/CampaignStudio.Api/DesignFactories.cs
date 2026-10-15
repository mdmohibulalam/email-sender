using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace CampaignStudio.Api;

public sealed class SqliteFactory : IDesignTimeDbContextFactory<SqliteStore>
{
    public SqliteStore CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SqliteStore>().UseSqlite("Data Source=:memory:").Options);
}
public sealed class PostgresFactory : IDesignTimeDbContextFactory<PostgresStore>
{
    public PostgresStore CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PostgresStore>().UseNpgsql("Host=localhost;Database=design_only;Username=design_only").Options);
}
