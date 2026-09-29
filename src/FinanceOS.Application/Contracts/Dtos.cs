using FinanceOS.Domain;

namespace FinanceOS.Application.Contracts;

public sealed record MoneyDto(
    long? Minor,
    decimal? Major,
    string CurrencyCode,
    string Formatted,
    string ProvenanceLabel,
    DateOnly? AsOf,
    string? Needed)
{
    public string Currency => CurrencyCode;
    public string Provenance => ProvenanceLabel;

    public static MoneyDto Of(long minor, Domain.Currency ccy, Domain.Provenance source, DateOnly? asOf = null) =>
        new(minor, minor / 100m, ccy.ToString().ToUpperInvariant(),
            Domain.Money.FromMajor(minor / 100m, ccy).Format(),
            Snake(source), asOf, null);

    public static MoneyDto Unknown(string needed, Domain.Currency ccy = Domain.Currency.Ngn) =>
        new(null, null, ccy.ToString().ToUpperInvariant(), "UNKNOWN", "unknown", null, needed);

    public static MoneyDto Estimate(long minor, Domain.Currency ccy, string note, DateOnly? asOf = null) =>
        new(minor, minor / 100m, ccy.ToString().ToUpperInvariant(),
            "ESTIMATE " + Domain.Money.FromMajor(minor / 100m, ccy).Format(),
            "estimate", asOf, note);

    private static string Snake(Domain.Provenance source) => source switch
    {
        Domain.Provenance.LastKnown => "last_known",
        _ => source.ToString().ToLowerInvariant()
    };
}

public sealed record AuthStatusDto(bool NeedsSetup, bool Authenticated, string? DisplayName);
public sealed record SetupRequest(string DisplayName, string Email, string Password);
public sealed record LoginRequest(string Email, string Password);

public sealed record AccountDto(
    Guid Id,
    string Slug,
    string Name,
    string Institution,
    string Role,
    string Job,
    string DoNotPutHere,
    string Health,
    string HealthSentence,
    MoneyDto? LatestBalance,
    string ReconciliationStatus);

public sealed record SnapshotRequest(decimal Amount, string Currency, DateOnly AsOf, string Provenance, string? Notes, string? Source = null);
public sealed record AssignmentRequest(Guid EnvelopeId, decimal Amount, DateOnly AsOf, string? Notes);

public sealed record TransactionDto(
    Guid Id,
    DateOnly Date,
    string Type,
    Guid AccountId,
    string AccountName,
    Guid? CounterpartyAccountId,
    MoneyDto Amount,
    MoneyDto Fee,
    string Category,
    string Description,
    string? Merchant,
    bool IsBusiness,
    bool IsTransfer,
    bool IsVoided,
    bool IsBetting);

public sealed record TransactionWriteRequest(
    DateOnly Date,
    string Type,
    Guid AccountId,
    Guid? CounterpartyAccountId,
    decimal Amount,
    decimal Fee,
    string Currency,
    Guid? CategoryId,
    Guid? EnvelopeId,
    Guid? GoalId,
    Guid? BusinessId,
    Guid? FamilyRecipientId,
    string Description,
    string? Merchant,
    string? Notes,
    string? Tags,
    bool IsBusiness,
    bool IsRecurring);

public sealed record PreviewDto(
    TransactionWriteRequest? Proposed,
    string Summary,
    IReadOnlyList<string> Questions,
    IReadOnlyList<string> BalanceImpacts,
    IReadOnlyList<string> BudgetImpacts,
    bool CanCommit);

public sealed record DashboardDto(
    MoneyDto NetWorth,
    MoneyDto LiquidCash,
    MoneyDto Investments,
    MoneyDto EmergencyFund,
    string EmergencySentence,
    MoneyDto HousingFund,
    string HousingSentence,
    MoneyDto MonthlyIncome,
    MoneyDto MonthlySpending,
    MoneyDto MonthlySavings,
    MoneyDto MonthlyInvestment,
    string? SavingsRateSentence,
    MoneyDto Spendable,
    string SpendableSentence,
    ThisMonthDto ThisMonth,
    IReadOnlyList<string> ActionRequired,
    IReadOnlyList<UnknownFactDto> Unknowns);

public sealed record UnknownFactDto(string Text, string Href);

public sealed record ThisMonthDto(
    MoneyDto Income,
    MoneyDto Expenses,
    MoneyDto Savings,
    MoneyDto Investments,
    MoneyDto FamilySupport,
    MoneyDto CareerBusiness,
    MoneyDto Betting,
    MoneyDto Transfers);

public sealed record ExplainDto(
    string Metric,
    string Sentence,
    string Formula,
    IReadOnlyList<ExplainLineDto> Lines,
    MoneyDto Result);

public sealed record ExplainLineDto(string Label, MoneyDto Amount, string? Note);

public sealed record EnvelopeDto(Guid Id, string Slug, string Name, string Class, string Purpose, string UseRule);
public sealed record GoalDto(
    Guid Id, string Slug, string Name, string Kind, MoneyDto Target, MoneyDto Current, string ProgressSentence,
    MoneyDto Monthly, DateOnly? Deadline, MoneyDto? RequiredMonthly, DateOnly? ProjectedCompletion, bool IsAspiration);

public sealed record HoldingDto(
    Guid Id,
    string Name,
    MoneyDto Amount,
    string Liquidity,
    string Purpose,
    string StatusNote,
    bool IsExpectedReceivable,
    decimal? UnitsHeld = null,
    decimal? CostBasisMajor = null,
    decimal? CurrentUnitPrice = null,
    string? Symbol = null,
    string? AssetClass = null,
    decimal? UnrealizedPnLMajor = null);

public sealed record BudgetItemDto(
    string Category, MoneyDto Budget, MoneyDto Actual, MoneyDto Remaining, string PercentUsed, string Status, string Sentence);

public sealed record MoneyMapNodeDto(
    string Id, string Label, string Kind, MoneyDto? Amount, string Purpose, string WhenToUse, string IfSpent, IReadOnlyList<string> Children);

public sealed record ActionDto(Guid Id, DateOnly ForDate, string Title, string Detail, string State);
public sealed record CalendarItemDto(DateOnly Date, string Title, string Kind, MoneyDto? Amount);
public sealed record AlertDto(string Priority, string Message);
public sealed record SubscriptionDto(
    Guid Id, string Name, MoneyDto Amount, string Frequency, Guid PaymentAccountId, DateOnly NextBillingDate,
    string Category, bool IsBusiness, bool IsActive);
public sealed record FamilySupportDto(
    Guid Id, string Recipient, DateOnly Date, MoneyDto Amount, string Kind, string Purpose);
public sealed record BusinessDto(
    Guid Id,
    string Slug,
    string Name,
    string? Description,
    MoneyDto Revenue,
    MoneyDto Expenses,
    MoneyDto Net,
    int ReinvestPercent,
    int PersonalPercent,
    bool IsActive,
    IReadOnlyList<BusinessLineDto> Lines);
public sealed record BusinessWriteRequest(
    string Name,
    string? Slug,
    string? Description,
    int ReinvestPercent,
    int PersonalPercent);
public sealed record LiabilityDto(
    Guid Id,
    string Slug,
    string Name,
    string Lender,
    MoneyDto Principal,
    MoneyDto Balance,
    decimal InterestRatePercent,
    MoneyDto MonthlyPayment,
    DateOnly? DueDate,
    string Provenance,
    string? Notes,
    bool IsActive);
public sealed record LiabilityWriteRequest(
    string Name,
    string? Slug,
    string Lender,
    decimal Principal,
    decimal Balance,
    string Currency,
    decimal InterestRatePercent,
    decimal MonthlyPayment,
    DateOnly? DueDate,
    string? Notes);
public sealed record CounterpartyLoanDto(
    Guid Id,
    string BorrowerName,
    MoneyDto Amount,
    MoneyDto BalanceRemaining,
    DateOnly LentDate,
    DateOnly? ExpectedRepaymentDate,
    string Status,
    string? Notes,
    bool IsActive);
public sealed record CounterpartyLoanWriteRequest(
    string BorrowerName,
    decimal Amount,
    decimal BalanceRemaining,
    string Currency,
    DateOnly LentDate,
    DateOnly? ExpectedRepaymentDate,
    string? Notes);
public sealed record BusinessLineDto(string Category, MoneyDto Amount);
public sealed record RuleDto(Guid Id, string Code, string Title, string Action, string Control, bool IsActive);
public sealed record ViolationDto(Guid Id, string Rule, string Message, DateOnly Date, bool IsOpen);
public sealed record ReportDto(string Name, string Period, string Summary, IReadOnlyList<ExplainLineDto> Lines);
public sealed record AssistantRequest(string Message);
public sealed record AssistantResponseDto(
    string Question, string Answer, IReadOnlyList<ExplainLineDto> Evidence, bool UsedEstimate, IReadOnlyList<string> MissingFacts);
public sealed record IncomePreviewDto(string PlanName, IReadOnlyList<ExplainLineDto> Lines, MoneyDto? Remainder, string Sentence, bool CanConfirm);
public sealed record ExchangeRateDto(string From, string To, decimal Rate, DateOnly AsOf, string Source);
public sealed record PensionDto(MoneyDto Balance, MoneyDto? Employee, MoneyDto? Employer, string Sentence);
public sealed record RetirementDto(string Sentence, IReadOnlyList<RetirementScenarioDto> Scenarios);
public sealed record RetirementScenarioDto(string Name, string Sentence, MoneyDto? FutureValue);
public sealed record AllocationLineDto(Guid Id, int SortOrder, string Name, string Kind, MoneyDto? Amount, string Rule);
public sealed record SettingsDto(
    string DisplayName,
    MoneyDto? OpayAllowance,
    int WatchPercent,
    int WarningPercent,
    int OverBudgetPercent,
    int BusinessReinvestPercent,
    int BusinessPersonalPercent,
    IReadOnlyList<FamilyLineDto> FamilyLines,
    bool HasMoved);
public sealed record FamilyLineDto(Guid Id, string Name, MoneyDto? RecurringAmount, string Purpose, bool IsRecurring);
public sealed record UpdateSettingsRequest(
    decimal? OpayAllowance,
    int? WatchPercent,
    int? WarningPercent,
    int? OverBudgetPercent,
    int? BusinessReinvestPercent,
    int? BusinessPersonalPercent,
    bool? HasMoved);
public sealed record RecurringItemDto(Guid Id, string Name, int DayOfMonth, MoneyDto? Amount, string Kind);

public sealed record StatementParseRequest(
    string Content,
    string? BankFormat = null,
    Guid? DefaultAccountId = null);

public sealed record ParsedTransactionDraftDto(
    int TempIndex,
    DateOnly Date,
    string Description,
    decimal Amount,
    string Currency,
    string Type,
    Guid? AccountId,
    string? AccountName,
    Guid? CounterpartyAccountId,
    string? CounterpartyAccountName,
    Guid? CategoryId,
    string? CategoryName,
    bool IsTransfer,
    bool IsDuplicate,
    string? DuplicateReason,
    bool NeedsReview,
    string RawNarration);

public sealed record StatementParseResultDto(
    string DetectedBank,
    int TotalParsed,
    int DuplicatesCount,
    int TransfersCount,
    IReadOnlyList<ParsedTransactionDraftDto> Items);

public sealed record StatementCommitRequest(
    IReadOnlyList<TransactionWriteRequest> Transactions);

public sealed record SpendingVelocityDto(
    MoneyDto AllowedBurnPerDayRemaining,
    MoneyDto ActualDailySpendVelocity,
    decimal PacingRatio,
    string PacingStatus,
    int DaysRemainingInMonth,
    int DaysElapsedInMonth,
    MoneyDto ProjectedMonthEndSpend,
    MoneyDto MonthToDateSpend,
    MoneyDto SpendablePool,
    string PacingSentence);

public sealed record FixedAssetDto(
    Guid Id,
    string Name,
    string Category,
    MoneyDto PurchasePrice,
    MoneyDto CurrentValuation,
    DateOnly PurchaseDate,
    int UsefulLifeMonths,
    MoneyDto SalvageValue,
    string? Notes,
    bool IncludeInNetWorth,
    bool IsActive);

public sealed record FixedAssetWriteRequest(
    string Name,
    string Category,
    decimal PurchasePrice,
    decimal CurrentValuation,
    string Currency,
    DateOnly PurchaseDate,
    int UsefulLifeMonths,
    decimal SalvageValue,
    string? Notes,
    bool IncludeInNetWorth);

public sealed record HoldingValuationUpdateRequest(
    decimal? UnitsHeld,
    decimal? CostBasisMajor,
    decimal? CurrentUnitPrice,
    string? Symbol,
    string? AssetClass,
    DateOnly AsOf);

public sealed record EnvelopeRebalanceRequest(
    Guid FromEnvelopeId,
    Guid ToEnvelopeId,
    decimal Amount,
    string? Notes);
