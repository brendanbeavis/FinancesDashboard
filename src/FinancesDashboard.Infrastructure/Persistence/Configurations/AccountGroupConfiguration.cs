using FinancesDashboard.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancesDashboard.Infrastructure.Persistence.Configurations;

public sealed class AccountGroupConfiguration : IEntityTypeConfiguration<AccountGroup>
{
    public void Configure(EntityTypeBuilder<AccountGroup> builder)
    {
        builder.ToTable("AccountGroups");

        builder.HasKey(accountGroup => accountGroup.Id);

        builder.Property(accountGroup => accountGroup.Name)
            .IsRequired();
    }
}
