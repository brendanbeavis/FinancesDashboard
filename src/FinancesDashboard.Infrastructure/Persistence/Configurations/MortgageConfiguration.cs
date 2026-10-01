using FinancesDashboard.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancesDashboard.Infrastructure.Persistence.Configurations;

public sealed class MortgageConfiguration : IEntityTypeConfiguration<Mortgage>
{
    public void Configure(EntityTypeBuilder<Mortgage> builder)
    {
        builder.ToTable("Mortgages");

        builder.HasKey(mortgage => mortgage.Id);

        builder.Property(mortgage => mortgage.Name)
            .IsRequired();

        builder.Property(mortgage => mortgage.OriginalPrincipal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(mortgage => mortgage.CurrentPrincipal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(mortgage => mortgage.AnnualInterestRate)
            .HasPrecision(9, 6)
            .IsRequired();

        builder.Property(mortgage => mortgage.ScheduledRepayment)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(mortgage => mortgage.RepaymentsPerYear)
            .IsRequired();

        builder.Property(mortgage => mortgage.StartDate)
            .IsRequired();
    }
}
