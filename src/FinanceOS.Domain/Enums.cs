namespace FinanceOS.Domain;

public enum AccountRole
{
    Clearing = 1,
    StrategicBuffer = 2,
    DailySpending = 3,
    SavingsVault = 4,
    GoalVault = 5,
    BusinessCard = 6,
    CashOnHand = 7,
    ExternalWallet = 8,
    InvestmentBroker = 9
}

public enum EnvelopeClass
{
    Spend = 1,
    Committed = 2,
    Savings = 3,
    Investment = 4,
    Emergency = 5,
    Housing = 6,
    Sinking = 7,
    Business = 8,
    Buffer = 9,
    DoNotTouch = 10
}

public enum TransactionType
{
    Income = 1,
    Expense = 2,
    Transfer = 3,
    Fee = 4,
    Investment = 5,
    Savings = 6,
    Debt = 7,
    Refund = 8,
    Withdrawal = 9,
    Deposit = 10,
    BusinessRevenue = 11,
    BusinessExpense = 12,
    Adjustment = 13
}

public enum AllocationKind
{
    Fixed = 1,
    Percent = 2,
    ActualCharge = 3,
    Remainder = 4
}

public enum IncomeReliability
{
    Reliable = 1,
    Strategic = 2
}

public enum ReconciliationStatus
{
    Reconciled = 1,
    Unreconciled = 2,
    Incomplete = 3
}

public enum GoalKind
{
    Emergency = 1,
    Housing = 2,
    NextRent = 3,
    Children = 4,
    Retirement = 5,
    OperatingReserve = 6,
    SinkingFund = 7
}

public enum ActionState
{
    Pending = 1,
    Completed = 2,
    Skipped = 3,
    Snoozed = 4
}

public enum AlertPriority
{
    Reconciliation = 1,
    CashFlow = 2,
    Overspending = 3,
    MissedSavings = 4,
    GoalDelay = 5,
    SubscriptionFunding = 6
}

public enum FamilySupportKind
{
    Recurring = 1,
    OneOff = 2
}

public enum SubscriptionFrequency
{
    Monthly = 1,
    Yearly = 2,
    Weekly = 3
}

public enum BudgetStatus
{
    Normal = 1,
    Watch = 2,
    Warning = 3,
    OverBudget = 4,
    Unknown = 5
}

public enum AccountHealth
{
    Healthy = 1,
    Watch = 2,
    Attention = 3,
    Unknown = 4
}
