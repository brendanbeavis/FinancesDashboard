using FinancesDashboard.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancesDashboard.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");

        builder.HasKey(account => account.Id);

        builder.Property(account => account.Name)
            .IsRequired();

        builder.Property(account => account.Type)
            .IsRequired();

        builder.Property(account => account.IsActive)
            .IsRequired();

        builder.Property(account => account.AccountNumber)
            .HasMaxLength(64);

        builder.Property(account => account.CurrencyCode)
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(account => account.Provider)
            .HasMaxLength(128);

        builder.Property(account => account.IncludeInNetWorth)
            .IsRequired();

        builder.Property(account => account.CreatedAt)
            .IsRequired();

        builder.Property(account => account.UpdatedAt)
            .IsRequired();

        builder.HasIndex(account => account.Type);
    }
}
