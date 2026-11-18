using FinancesDashboard.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancesDashboard.Infrastructure.Persistence.Configurations;

public sealed class PortfolioHoldingConfiguration : IEntityTypeConfiguration<PortfolioHolding>
{
    public void Configure(EntityTypeBuilder<PortfolioHolding> builder)
    {
        builder.ToTable("PortfolioHoldings");

        builder.HasKey(holding => holding.Id);

        builder.Property(holding => holding.Units)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(holding => holding.AverageCostPerUnit)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(holding => holding.TotalCostBase)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(holding => holding.CurrentPrice)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(holding => holding.CurrentMarketValue)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(holding => holding.UnrealisedProfitLoss)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(holding => holding.UnrealisedProfitLossPercentage)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(holding => holding.CurrencyCode)
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(holding => holding.LastUpdatedAt)
            .IsRequired();

        builder.Property(holding => holding.IsClosed)
            .IsRequired();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(holding => holding.InvestmentAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<InvestmentInstrument>()
            .WithMany()
            .HasForeignKey(holding => holding.InvestmentInstrumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(holding => new { holding.InvestmentAccountId, holding.InvestmentInstrumentId })
            .IsUnique();

        builder.HasIndex(holding => holding.InvestmentAccountId);
    }
}
