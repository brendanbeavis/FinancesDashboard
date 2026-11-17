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

    public DbSet<MortgageOffsetAccount> MortgageOffsetAccounts => Set<MortgageOffsetAccount>();

    public DbSet<InterestRatePeriod> InterestRatePeriods => Set<InterestRatePeriod>();

    public DbSet<InvestmentInstrument> InvestmentInstruments => Set<InvestmentInstrument>();

    public DbSet<PortfolioHolding> PortfolioHoldings => Set<PortfolioHolding>();

    public DbSet<PortfolioSnapshot> PortfolioSnapshots => Set<PortfolioSnapshot>();

    public DbSet<HoldingSnapshot> HoldingSnapshots => Set<HoldingSnapshot>();

    public DbSet<PortfolioImportBatch> PortfolioImportBatches => Set<PortfolioImportBatch>();

    public DbSet<PortfolioImportIssue> PortfolioImportIssues => Set<PortfolioImportIssue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AccountConfiguration());
        modelBuilder.ApplyConfiguration(new TransactionConfiguration());
        modelBuilder.ApplyConfiguration(new AccountGroupConfiguration());
        modelBuilder.ApplyConfiguration(new AccountGroupMemberConfiguration());
        modelBuilder.ApplyConfiguration(new MortgageConfiguration());
        modelBuilder.ApplyConfiguration(new MortgageOffsetAccountConfiguration());
        modelBuilder.ApplyConfiguration(new InterestRatePeriodConfiguration());
        modelBuilder.ApplyConfiguration(new InvestmentInstrumentConfiguration());
        modelBuilder.ApplyConfiguration(new PortfolioHoldingConfiguration());
        modelBuilder.ApplyConfiguration(new PortfolioSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new HoldingSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new PortfolioImportBatchConfiguration());
        modelBuilder.ApplyConfiguration(new PortfolioImportIssueConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
