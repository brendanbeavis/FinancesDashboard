using FinancesDashboard.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancesDashboard.Infrastructure.Persistence.Configurations;

public sealed class PortfolioImportBatchConfiguration : IEntityTypeConfiguration<PortfolioImportBatch>
{
    public void Configure(EntityTypeBuilder<PortfolioImportBatch> builder)
    {
        builder.ToTable("PortfolioImportBatches");

        builder.HasKey(batch => batch.Id);

        builder.Property(batch => batch.FileName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(batch => batch.FileHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(batch => batch.ImportDateTime)
            .IsRequired();

        builder.Property(batch => batch.SnapshotDateTime)
            .IsRequired();

        builder.Property(batch => batch.TotalMarketValue)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(batch => batch.TotalCostBase)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(batch => batch.ImportStatus)
            .IsRequired();

        builder.Property(batch => batch.ErrorSummary)
            .HasMaxLength(2048);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(batch => batch.InvestmentAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(batch => new { batch.InvestmentAccountId, batch.FileHash })
            .IsUnique();

        builder.HasIndex(batch => new { batch.InvestmentAccountId, batch.SnapshotDateTime });
    }
}
