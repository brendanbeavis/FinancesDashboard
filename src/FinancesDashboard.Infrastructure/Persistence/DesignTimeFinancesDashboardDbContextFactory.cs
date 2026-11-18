using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinancesDashboard.Infrastructure.Persistence;

public sealed class DesignTimeFinancesDashboardDbContextFactory : IDesignTimeDbContextFactory<FinancesDashboardDbContext>
{
    public FinancesDashboardDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FinancesDashboardDbContext>();
        optionsBuilder.UseSqlite("Data Source=financesdashboard.db");

        return new FinancesDashboardDbContext(optionsBuilder.Options);
    }
}
