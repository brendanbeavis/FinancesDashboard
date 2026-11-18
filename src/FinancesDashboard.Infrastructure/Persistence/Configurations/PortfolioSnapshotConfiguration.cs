using FinancesDashboard.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancesDashboard.Infrastructure.Persistence.Configurations;

public sealed class PortfolioSnapshotConfiguration : IEntityTypeConfiguration<PortfolioSnapshot>
{
    public void Configure(EntityTypeBuilder<PortfolioSnapshot> builder)
    {
        builder.ToTable("PortfolioSnapshots");

        builder.HasKey(snapshot => snapshot.Id);

        builder.Property(snapshot => snapshot.SnapshotDateTime)
            .IsRequired();

        builder.Property(snapshot => snapshot.CurrencyCode)
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(snapshot => snapshot.TotalMarketValue)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(snapshot => snapshot.TotalCostBase)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(snapshot => snapshot.TotalUnrealisedProfitLoss)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(snapshot => snapshot.TotalUnrealisedProfitLossPercentage)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(snapshot => snapshot.TotalDailyValueChange)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(snapshot => snapshot.Source)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(snapshot => snapshot.CreatedAt)
            .IsRequired();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(snapshot => snapshot.InvestmentAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(snapshot => new { snapshot.InvestmentAccountId, snapshot.SnapshotDateTime })
            .IsUnique();
    }
}
