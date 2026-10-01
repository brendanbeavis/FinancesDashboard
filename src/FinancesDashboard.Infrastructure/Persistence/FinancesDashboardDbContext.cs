using FinancesDashboard.Domain.Models;
using FinancesDashboard.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Infrastructure.Persistence;

public sealed class FinancesDashboardDbContext(DbContextOptions<FinancesDashboardDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<AccountGroup> AccountGroups => Set<AccountGroup>();

    public DbSet<AccountGroupMember> AccountGroupMembers => Set<AccountGroupMember>();

    public DbSet<Mortgage> Mortgages => Set<Mortgage>();

    public DbSet<InterestRatePeriod> InterestRatePeriods => Set<InterestRatePeriod>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AccountConfiguration());
        modelBuilder.ApplyConfiguration(new TransactionConfiguration());
        modelBuilder.ApplyConfiguration(new AccountGroupConfiguration());
        modelBuilder.ApplyConfiguration(new AccountGroupMemberConfiguration());
        modelBuilder.ApplyConfiguration(new MortgageConfiguration());
        modelBuilder.ApplyConfiguration(new InterestRatePeriodConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
