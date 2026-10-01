using FinancesDashboard.Domain.Enums;
using FinancesDashboard.Domain.Models;

namespace FinancesDashboard.Tests;

public class DomainModelTests
{
    [Fact]
    public void AccountType_HasExpectedValues()
    {
        Assert.Equal(0, (int)AccountType.Transaction);
        Assert.Equal(1, (int)AccountType.Savings);
        Assert.Equal(2, (int)AccountType.CreditCard);
        Assert.Equal(3, (int)AccountType.Mortgage);
        Assert.Equal(4, (int)AccountType.Investment);
        Assert.Equal(5, (int)AccountType.Other);
    }

    [Fact]
    public void Account_CanAssignAllProperties()
    {
        var id = Guid.NewGuid();

        var account = new Account
        {
            Id = id,
            Name = "Everyday Account",
            Type = AccountType.Transaction,
            IsActive = true
        };

        Assert.Equal(id, account.Id);
        Assert.Equal("Everyday Account", account.Name);
        Assert.Equal(AccountType.Transaction, account.Type);
        Assert.True(account.IsActive);
    }

    [Fact]
    public void AccountGroup_CanAssignAllProperties()
    {
        var id = Guid.NewGuid();

        var group = new AccountGroup
        {
            Id = id,
            Name = "Household"
        };

        Assert.Equal(id, group.Id);
        Assert.Equal("Household", group.Name);
    }

    [Fact]
    public void AccountGroupMember_CanAssignAllProperties()
    {
        var groupId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var member = new AccountGroupMember
        {
            AccountGroupId = groupId,
            AccountId = accountId
        };

        Assert.Equal(groupId, member.AccountGroupId);
        Assert.Equal(accountId, member.AccountId);
    }

    [Fact]
    public void Transaction_CanAssignAllProperties()
    {
        var id = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var importedUtc = DateTime.UtcNow;

        var transaction = new Transaction
        {
            Id = id,
            AccountId = accountId,
            Date = new DateOnly(2026, 1, 15),
            Amount = -125.45m,
            Description = "Groceries",
            BankBalance = 2500.55m,
            ImportHash = "abc123",
            ImportedUtc = importedUtc
        };

        Assert.Equal(id, transaction.Id);
        Assert.Equal(accountId, transaction.AccountId);
        Assert.Equal(new DateOnly(2026, 1, 15), transaction.Date);
        Assert.Equal(-125.45m, transaction.Amount);
        Assert.Equal("Groceries", transaction.Description);
        Assert.Equal(2500.55m, transaction.BankBalance);
        Assert.Equal("abc123", transaction.ImportHash);
        Assert.Equal(importedUtc, transaction.ImportedUtc);
    }

    [Fact]
    public void Mortgage_CanAssignAllProperties()
    {
        var id = Guid.NewGuid();

        var mortgage = new Mortgage
        {
            Id = id,
            Name = "Home Loan",
            OriginalPrincipal = 600000m,
            CurrentPrincipal = 550000m,
            AnnualInterestRate = 0.0615m,
            ScheduledRepayment = 3200m,
            RepaymentsPerYear = 12,
            StartDate = new DateOnly(2024, 7, 1),
            ExpectedEndDate = new DateOnly(2054, 7, 1)
        };

        Assert.Equal(id, mortgage.Id);
        Assert.Equal("Home Loan", mortgage.Name);
        Assert.Equal(600000m, mortgage.OriginalPrincipal);
        Assert.Equal(550000m, mortgage.CurrentPrincipal);
        Assert.Equal(0.0615m, mortgage.AnnualInterestRate);
        Assert.Equal(3200m, mortgage.ScheduledRepayment);
        Assert.Equal(12, mortgage.RepaymentsPerYear);
        Assert.Equal(new DateOnly(2024, 7, 1), mortgage.StartDate);
        Assert.Equal(new DateOnly(2054, 7, 1), mortgage.ExpectedEndDate);
    }

    [Fact]
    public void InterestRatePeriod_CanAssignAllProperties()
    {
        var mortgageId = Guid.NewGuid();

        var period = new InterestRatePeriod
        {
            MortgageId = mortgageId,
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2025, 12, 31),
            AnnualInterestRate = 0.0599m
        };

        Assert.Equal(mortgageId, period.MortgageId);
        Assert.Equal(new DateOnly(2025, 1, 1), period.StartDate);
        Assert.Equal(new DateOnly(2025, 12, 31), period.EndDate);
        Assert.Equal(0.0599m, period.AnnualInterestRate);
    }
}
