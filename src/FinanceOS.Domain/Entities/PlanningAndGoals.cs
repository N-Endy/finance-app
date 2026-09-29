namespace FinanceOS.Domain.Entities;

public sealed class IncomeSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long ExpectedAmountMinor { get; set; }
    public Currency Currency { get; set; } = Currency.Ngn;
    public int ExpectedDayOfMonth { get; set; }
    public IncomeReliability Reliability { get; set; }
    public Guid DestinationAccountId { get; set; }
    public string Rule { get; set; } = string.Empty;
}

public sealed class AllocationPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid IncomeSourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<AllocationLine> Lines { get; set; } = [];
    public IncomeSource? IncomeSource { get; set; }
}

public sealed class AllocationLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AllocationPlanId { get; set; }
    public int SortOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public AllocationKind Kind { get; set; }
    public long? AmountMinor { get; set; }
    public Currency Currency { get; set; } = Currency.Ngn;
    public Guid? EnvelopeId { get; set; }
    public Guid? DestinationAccountId { get; set; }
    public Guid? GoalId { get; set; }
    public string? ActualChargeKey { get; set; }
    public string Rule { get; set; } = string.Empty;
    public Envelope? Envelope { get; set; }
}

public sealed class FamilyRecipient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public long? RecurringAmountMinor { get; set; }
    public bool IsRecurring { get; set; }
}

public sealed class FamilySupport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid RecipientId { get; set; }
    public Guid? TransactionId { get; set; }
    public DateOnly Date { get; set; }
    public long AmountMinor { get; set; }
    public Currency Currency { get; set; } = Currency.Ngn;
    public FamilySupportKind Kind { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public FamilyRecipient? Recipient { get; set; }
}

public sealed class Goal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public GoalKind Kind { get; set; }
    public long TargetMinor { get; set; }
    public Currency Currency { get; set; } = Currency.Ngn;
    public long MonthlyContributionMinor { get; set; }
    public DateOnly? Deadline { get; set; }
    public Guid? FundingAccountId { get; set; }
    public string Rule { get; set; } = string.Empty;
    public bool IsAspiration { get; set; }
    public bool IsActive { get; set; } = true;
    public bool ConvertedFromHousing { get; set; }
}

public sealed class Holding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? AccountId { get; set; }
    public long AmountMinor { get; set; }
    public Currency Currency { get; set; }
    public Provenance Provenance { get; set; }
    public DateOnly AsOf { get; set; }
    public long MonthlyContributionMinor { get; set; }
    public string Liquidity { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string StatusNote { get; set; } = string.Empty;
    public bool IncludeInNetWorth { get; set; } = true;
    public bool IsExpectedReceivable { get; set; }
    public decimal? UnitsHeld { get; set; }
    public decimal? CostBasisMajor { get; set; }
    public decimal? CurrentUnitPrice { get; set; }
    public string? AssetClass { get; set; }
    public string? Symbol { get; set; }
}

public sealed class ExchangeRate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Currency From { get; set; }
    public Currency To { get; set; }
    public decimal Rate { get; set; }
    public DateOnly AsOf { get; set; }
    public string Source { get; set; } = "manual";
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public Currency Currency { get; set; }
    public SubscriptionFrequency Frequency { get; set; }
    public Guid PaymentAccountId { get; set; }
    public DateOnly NextBillingDate { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsBusiness { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class RecurringItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DayOfMonth { get; set; }
    public long? AmountMinor { get; set; }
    public Currency Currency { get; set; } = Currency.Ngn;
    public Guid? AccountId { get; set; }
    public Guid? EnvelopeId { get; set; }
    public string Kind { get; set; } = "transfer";
    public bool IsActive { get; set; } = true;
}

public sealed class Business
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ReinvestPercent { get; set; } = 70;
    public int PersonalPercent { get; set; } = 30;
    public bool IsActive { get; set; } = true;
    public string? BaselineCostsJson { get; set; }
}

public sealed class Liability
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Lender { get; set; } = string.Empty;
    public long PrincipalMinor { get; set; }
    public long BalanceMinor { get; set; }
    public Currency Currency { get; set; } = Currency.Ngn;
    public decimal InterestRatePercent { get; set; }
    public long MonthlyPaymentMinor { get; set; }
    public DateOnly? DueDate { get; set; }
    public Provenance Provenance { get; set; } = Provenance.Confirmed;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CounterpartyLoan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string BorrowerName { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public long BalanceRemainingMinor { get; set; }
    public Currency Currency { get; set; } = Currency.Ngn;
    public DateOnly LentDate { get; set; }
    public DateOnly? ExpectedRepaymentDate { get; set; }
    public string Status { get; set; } = "Active";
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class FinancialRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Control { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class RuleViolation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid RuleId { get; set; }
    public DateOnly Date { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? TransactionId { get; set; }
    public bool IsOpen { get; set; } = true;
}

public sealed class FinancialAction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public DateOnly ForDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public ActionState State { get; set; } = ActionState.Pending;
    public int SortOrder { get; set; }
    public DateOnly? SnoozeUntil { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}

public sealed class PensionAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public long? CurrentBalanceMinor { get; set; }
    public Currency Currency { get; set; } = Currency.Ngn;
    public long? EmployeeContributionMinor { get; set; }
    public long? EmployerContributionMinor { get; set; }
    public decimal? AssumedReturn { get; set; }
    public int? RetirementAge { get; set; }
    public Provenance Provenance { get; set; } = Provenance.Unknown;
}

public sealed class RetirementAssumption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public int? CurrentAge { get; set; }
    public int? RetirementAge { get; set; }
    public long? MonthlyContributionMinor { get; set; }
    public decimal? ConservativeReturn { get; set; }
    public decimal? BaseReturn { get; set; }
    public decimal? AggressiveReturn { get; set; }
    public decimal? Inflation { get; set; }
    public long? DesiredAnnualSpendingMinor { get; set; }
    public decimal? WithdrawalRate { get; set; }
    public bool InvestmentsConfirmed { get; set; }
}

public sealed class ActualCharge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Key { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public Currency Currency { get; set; } = Currency.Ngn;
    public DateOnly AsOf { get; set; }
}

public sealed class FixedAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Gadget, Equipment, Vehicle, Property
    public long PurchasePriceMinor { get; set; }
    public long CurrentValuationMinor { get; set; }
    public Currency Currency { get; set; } = Currency.Ngn;
    public DateOnly PurchaseDate { get; set; }
    public int UsefulLifeMonths { get; set; } = 36;
    public long SalvageValueMinor { get; set; } = 0;
    public string? Notes { get; set; }
    public bool IncludeInNetWorth { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

