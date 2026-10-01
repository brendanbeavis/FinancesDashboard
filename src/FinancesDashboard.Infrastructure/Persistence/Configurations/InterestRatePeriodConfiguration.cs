using FinancesDashboard.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancesDashboard.Infrastructure.Persistence.Configurations;

public sealed class InterestRatePeriodConfiguration : IEntityTypeConfiguration<InterestRatePeriod>
{
    public void Configure(EntityTypeBuilder<InterestRatePeriod> builder)
    {
        builder.ToTable("InterestRatePeriods");

        builder.HasKey(interestRatePeriod => new { interestRatePeriod.MortgageId, interestRatePeriod.StartDate });

        builder.Property(interestRatePeriod => interestRatePeriod.StartDate)
            .IsRequired();

        builder.Property(interestRatePeriod => interestRatePeriod.AnnualInterestRate)
            .HasPrecision(9, 6)
            .IsRequired();

        builder.HasOne<Mortgage>()
            .WithMany()
            .HasForeignKey(interestRatePeriod => interestRatePeriod.MortgageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
