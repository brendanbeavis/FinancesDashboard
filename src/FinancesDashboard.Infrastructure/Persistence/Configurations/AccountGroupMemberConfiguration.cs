using FinancesDashboard.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancesDashboard.Infrastructure.Persistence.Configurations;

public sealed class AccountGroupMemberConfiguration : IEntityTypeConfiguration<AccountGroupMember>
{
    public void Configure(EntityTypeBuilder<AccountGroupMember> builder)
    {
        builder.ToTable("AccountGroupMembers");

        builder.HasKey(accountGroupMember => new { accountGroupMember.AccountGroupId, accountGroupMember.AccountId });

        builder.HasOne<AccountGroup>()
            .WithMany()
            .HasForeignKey(accountGroupMember => accountGroupMember.AccountGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(accountGroupMember => accountGroupMember.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
