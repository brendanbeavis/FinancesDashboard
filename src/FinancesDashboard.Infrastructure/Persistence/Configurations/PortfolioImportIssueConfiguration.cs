using FinancesDashboard.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancesDashboard.Infrastructure.Persistence.Configurations;

public sealed class PortfolioImportIssueConfiguration : IEntityTypeConfiguration<PortfolioImportIssue>
{
    public void Configure(EntityTypeBuilder<PortfolioImportIssue> builder)
    {
        builder.ToTable("PortfolioImportIssues");

        builder.HasKey(issue => issue.Id);

        builder.Property(issue => issue.RowNumber)
            .IsRequired();

        builder.Property(issue => issue.InstrumentCode)
            .HasMaxLength(32);

        builder.Property(issue => issue.ErrorMessage)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(issue => issue.RawRow)
            .HasMaxLength(2048)
            .IsRequired();

        builder.HasOne<PortfolioImportBatch>()
            .WithMany()
            .HasForeignKey(issue => issue.PortfolioImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(issue => issue.PortfolioImportBatchId);
    }
}
