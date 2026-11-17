using FinancesDashboard.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancesDashboard.Infrastructure.Persistence.Configurations;

public sealed class InvestmentInstrumentConfiguration : IEntityTypeConfiguration<InvestmentInstrument>
{
    public void Configure(EntityTypeBuilder<InvestmentInstrument> builder)
    {
        builder.ToTable("InvestmentInstruments");

        builder.HasKey(instrument => instrument.Id);

        builder.Property(instrument => instrument.Code)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(instrument => instrument.Name)
            .HasMaxLength(256);

        builder.Property(instrument => instrument.Exchange)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(instrument => instrument.CurrencyCode)
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(instrument => instrument.IsActive)
            .IsRequired();

        builder.HasIndex(instrument => new { instrument.Exchange, instrument.Code })
            .IsUnique();
    }
}
