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

    public async Task<IReadOnlyList<AccountDto>> GetAccountsByTypeAsync(AccountType accountType, CancellationToken cancellationToken)
    {
        return await dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.Type == accountType)
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
        return await GetAccountTransactionsAsync(new TransactionQueryFilterRequest { AccountId = accountId }, cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetAccountTransactionsAsync(TransactionQueryFilterRequest filter, CancellationToken cancellationToken)
    {
        var query = dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == filter.AccountId);

        // Apply date filters
        if (filter.DateFrom.HasValue)
        {
            query = query.Where(t => t.Date >= filter.DateFrom);
        }

        if (filter.DateTo.HasValue)
        {
            query = query.Where(t => t.Date <= filter.DateTo);
        }

        // Apply description filter
        if (!string.IsNullOrWhiteSpace(filter.DescriptionContains))
        {
            var searchTerm = filter.DescriptionContains.ToLower();
            query = query.Where(t => t.Description.ToLower().Contains(searchTerm));
        }

        // Apply amount filters
        if (filter.MinAmount.HasValue)
        {
            query = query.Where(t => t.Amount >= filter.MinAmount);
        }

        if (filter.MaxAmount.HasValue)
        {
            query = query.Where(t => t.Amount <= filter.MaxAmount);
        }

        // Apply transaction direction filter
        if (!string.IsNullOrEmpty(filter.TransactionDirection))
        {
            if (filter.TransactionDirection == "Positive")
            {
                query = query.Where(t => t.Amount > 0m);
            }
            else if (filter.TransactionDirection == "Negative")
            {
                query = query.Where(t => t.Amount < 0m);
            }
        }

        // Apply sorting
        query = ApplySort(query, filter.SortBy, filter.SortOrder);

        return await query.ToListAsync(cancellationToken);
    }

    private static IQueryable<Transaction> ApplySort(IQueryable<Transaction> query, string? sortBy, string? sortOrder)
    {
        // Normalize inputs
        var column = !string.IsNullOrEmpty(sortBy) ? sortBy : "Date";
        var order = !string.IsNullOrEmpty(sortOrder) ? sortOrder : "Desc";
        var isAscending = order == "Asc";

        // Apply primary sort
        query = column switch
        {
            "Description" => isAscending 
                ? query.OrderBy(t => t.Description).ThenByDescending(t => t.Date).ThenByDescending(t => t.ImportedUtc)
                : query.OrderByDescending(t => t.Description).ThenByDescending(t => t.Date).ThenByDescending(t => t.ImportedUtc),

            "Amount" => isAscending
                ? query.OrderBy(t => t.Amount).ThenByDescending(t => t.Date).ThenByDescending(t => t.ImportedUtc)
                : query.OrderByDescending(t => t.Amount).ThenByDescending(t => t.Date).ThenByDescending(t => t.ImportedUtc),

            "BankBalance" => isAscending
                ? query.OrderBy(t => t.BankBalance).ThenByDescending(t => t.Date).ThenByDescending(t => t.ImportedUtc)
                : query.OrderByDescending(t => t.BankBalance).ThenByDescending(t => t.Date).ThenByDescending(t => t.ImportedUtc),

            "Date" or _ => isAscending
                ? query.OrderBy(t => t.Date).ThenByDescending(t => t.ImportedUtc)
                : query.OrderByDescending(t => t.Date).ThenByDescending(t => t.ImportedUtc),
        };

        return query;
    }

    public async Task DeleteTransactionAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Transactions.FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);
        if (transaction is null)
        {
            throw new KeyNotFoundException($"Transaction with id '{transactionId}' not found.");
        }

        dbContext.Transactions.Remove(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
