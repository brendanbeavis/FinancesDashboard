using FinancesDashboard.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancesDashboard.Infrastructure.Persistence.Configurations;

public sealed class MortgageOffsetAccountConfiguration : IEntityTypeConfiguration<MortgageOffsetAccount>
{
    public void Configure(EntityTypeBuilder<MortgageOffsetAccount> builder)
    {
        builder.ToTable("MortgageOffsetAccounts");

        builder.HasKey(link => new { link.MortgageAccountId, link.OffsetAccountId });

        builder.Property(link => link.MortgageAccountId)
            .IsRequired();

        builder.Property(link => link.OffsetAccountId)
            .IsRequired();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(link => link.MortgageAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(link => link.OffsetAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
