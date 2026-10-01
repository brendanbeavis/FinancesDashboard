using FinancesDashboard.Application.Dto;
using FinancesDashboard.Domain.Enums;
using FinancesDashboard.Domain.Models;
using FinancesDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancesDashboard.Application.Services;

public sealed class AccountApplicationService(FinancesDashboardDbContext dbContext)
{
    public async Task<IReadOnlyList<AccountDto>> GetAccountsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Accounts
            .AsNoTracking()
            .OrderBy(account => account.Name)
            .Select(account => new AccountDto(account.Id, account.Name, account.Type.ToString(), account.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<AccountDto> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AccountType>(request.Type, ignoreCase: true, out var accountType))
        {
            throw new ArgumentException($"Unknown account type '{request.Type}'.", nameof(request));
        }

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = accountType,
            IsActive = true
        };

        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AccountDto(account.Id, account.Name, account.Type.ToString(), account.IsActive);
    }
}
