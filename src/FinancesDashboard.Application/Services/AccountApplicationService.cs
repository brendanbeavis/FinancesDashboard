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

    public async Task<AccountDto> UpdateAccountAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account is null)
        {
            throw new KeyNotFoundException($"Account with id '{id}' not found.");
        }

        account.Name = request.Name;
        account.IsActive = request.IsActive;

        dbContext.Accounts.Update(account);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AccountDto(account.Id, account.Name, account.Type.ToString(), account.IsActive);
    }

    public async Task DeleteAccountAsync(Guid id, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account is null)
        {
            throw new KeyNotFoundException($"Account with id '{id}' not found.");
        }

        dbContext.Accounts.Remove(account);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetAccountTransactionsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return await dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.Date)
            .ToListAsync(cancellationToken);
    }
}
