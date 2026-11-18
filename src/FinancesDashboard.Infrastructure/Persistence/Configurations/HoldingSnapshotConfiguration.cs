using FinancesDashboard.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancesDashboard.Infrastructure.Persistence.Configurations;

public sealed class HoldingSnapshotConfiguration : IEntityTypeConfiguration<HoldingSnapshot>
{
    public void Configure(EntityTypeBuilder<HoldingSnapshot> builder)
    {
        builder.ToTable("HoldingSnapshots");

        builder.HasKey(snapshot => snapshot.Id);

        builder.Property(snapshot => snapshot.Units)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(snapshot => snapshot.AverageCostPerUnit)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(snapshot => snapshot.TotalCostBase)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(snapshot => snapshot.MarketPrice)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(snapshot => snapshot.MarketValue)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(snapshot => snapshot.DailyPriceChange)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(snapshot => snapshot.DailyPriceChangePercentage)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(snapshot => snapshot.UnrealisedProfitLoss)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(snapshot => snapshot.UnrealisedProfitLossPercentage)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(snapshot => snapshot.PortfolioWeightPercentage)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.HasOne<PortfolioSnapshot>()
            .WithMany()
            .HasForeignKey(snapshot => snapshot.PortfolioSnapshotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<InvestmentInstrument>()
            .WithMany()
            .HasForeignKey(snapshot => snapshot.InvestmentInstrumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(snapshot => new { snapshot.PortfolioSnapshotId, snapshot.InvestmentInstrumentId })
            .IsUnique();
    }
}
