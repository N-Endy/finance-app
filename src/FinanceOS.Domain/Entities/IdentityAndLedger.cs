namespace FinanceOS.Domain.Entities;

public sealed class Owner
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = "Nnamdi";
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool PlanSeeded { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public int? Age { get; set; }
    public bool HasMoved { get; set; }
    public long? OpayMonthlyAllowanceMinor { get; set; }
    public int WatchPercent { get; set; } = 70;
    public int WarningPercent { get; set; } = 90;
    public int OverBudgetPercent { get; set; } = 100;
    public int BusinessReinvestPercent { get; set; } = 70;
    public int BusinessPersonalPercent { get; set; } = 30;
    public string? HomeSavingsLockNote { get; set; }
    public DateOnly? HomeSavingsLockUntil { get; set; }
}

public sealed class FinancialAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Institution { get; set; } = string.Empty;
    public AccountRole Role { get; set; }
    public Currency Currency { get; set; } = Currency.Ngn;
    public string Job { get; set; } = string.Empty;
    public string DoNotPutHere { get; set; } = string.Empty;
    public string WhenToUse { get; set; } = string.Empty;
    public string OperatingRule { get; set; } = string.Empty;
    public long? FloorMinor { get; set; }
    public long? CeilingMinor { get; set; }
    public long? MonthlyAllowanceMinor { get; set; }
    public int? BillingCycleDay { get; set; }
    public long? CreditLimitMinor { get; set; }
    public bool AllowsDailySpending { get; set; }
    public bool IncludeInNetWorth { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public List<BalanceSnapshot> Snapshots { get; set; } = [];
    public List<EnvelopeAssignment> Assignments { get; set; } = [];
}

public sealed class BalanceSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public long AmountMinor { get; set; }
    public Currency Currency { get; set; }
    public DateOnly AsOf { get; set; }
    public Provenance Provenance { get; set; }
    public string Source { get; set; } = "manual";
    public string? Notes { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Envelope
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public EnvelopeClass Class { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string UseRule { get; set; } = string.Empty;
    public Guid? DefaultFundingAccountId { get; set; }
    public Guid? GoalId { get; set; }
    public bool IsEssential { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class EnvelopeAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public Guid EnvelopeId { get; set; }
    public long AmountMinor { get; set; }
    public Currency Currency { get; set; }
    public DateOnly AsOf { get; set; }
    public string? Notes { get; set; }
    public Envelope? Envelope { get; set; }
}

public sealed class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentSlug { get; set; }
    public Guid? EnvelopeId { get; set; }
    public bool IsBetting { get; set; }
    public bool IsFamily { get; set; }
    public bool IsBusiness { get; set; }
    public bool IsTransfer { get; set; }
}

public sealed class LedgerTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public DateOnly Date { get; set; }
    public TransactionType Type { get; set; }
    public Guid AccountId { get; set; }
    public Guid? CounterpartyAccountId { get; set; }
    public long AmountMinor { get; set; }
    public long FeeMinor { get; set; }
    public Currency Currency { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? EnvelopeId { get; set; }
    public Guid? GoalId { get; set; }
    public Guid? BusinessId { get; set; }
    public Guid? FamilyRecipientId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Merchant { get; set; }
    public string? Notes { get; set; }
    public string? Tags { get; set; }
    public bool IsBusiness { get; set; }
    public bool IsRecurring { get; set; }
    public bool IsTransfer { get; set; }
    public bool IsVoided { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<Posting> Postings { get; set; } = [];
    public List<TransactionRevision> Revisions { get; set; } = [];
    public FinancialAccount? Account { get; set; }
    public Category? Category { get; set; }
    public Envelope? Envelope { get; set; }
}

public sealed class Posting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransactionId { get; set; }
    public Guid AccountId { get; set; }
    public long AmountMinor { get; set; }
    public Currency Currency { get; set; }
    public string Role { get; set; } = "movement";
}

public sealed class TransactionRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransactionId { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    public string Actor { get; set; } = "owner";
    public string Action { get; set; } = "edit";
    public string BeforeJson { get; set; } = "{}";
    public string AfterJson { get; set; } = "{}";
}

public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? OwnerId { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    public string Actor { get; set; } = "owner";
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string Details { get; set; } = string.Empty;
}

public sealed class ExternalNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Provenance Provenance { get; set; } = Provenance.LastKnown;
    public DateOnly? AsOf { get; set; }
}
