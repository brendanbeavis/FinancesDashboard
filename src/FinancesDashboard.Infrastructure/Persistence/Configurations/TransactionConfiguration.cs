using FinancesDashboard.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancesDashboard.Infrastructure.Persistence.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");

        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.Date)
            .IsRequired();

        builder.Property(transaction => transaction.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(transaction => transaction.Description)
            .IsRequired();

        builder.Property(transaction => transaction.BankBalance)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(transaction => transaction.ImportHash)
            .IsRequired();

        builder.Property(transaction => transaction.ImportedUtc)
            .IsRequired();

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(transaction => transaction.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(transaction => transaction.AccountId);
        builder.HasIndex(transaction => transaction.Date);
        builder.HasIndex(transaction => new { transaction.AccountId, transaction.Date });
        builder.HasIndex(transaction => transaction.ImportHash)
            .IsUnique();
    }
}
