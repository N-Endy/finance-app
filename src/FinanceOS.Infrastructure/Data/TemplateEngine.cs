using FinanceOS.Application.Contracts;
using FinanceOS.Domain;
using FinanceOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceOS.Infrastructure.Data;

public static class TemplateEngine
{
    public static readonly IReadOnlyList<OnboardingTemplateDto> AvailableTemplates =
    [
        new(
            "zero_based",
            "Zero-Based Budgeting",
            "Give every dollar or naira a job before the month begins.",
            "Envelope budgeting where all income is allocated down to zero across essential living expenses, discretionary spending, sinking funds, and emergency savings.",
            ["Primary Checking", "Emergency Vault", "Sinking Fund Vault"],
            ["Housing & Rent", "Groceries & Nutrition", "Utilities & Bills", "Transportation", "Discretionary & Dining", "Emergency Vault", "Sinking Buffer"]
        ),
        new(
            "fifty_thirty_twenty",
            "50 / 30 / 20 Rule",
            "Simple, balanced framework: 50% Needs, 30% Wants, 20% Savings.",
            "Classic balanced budget separating essential non-negotiables, lifestyle spending, and long-term savings goals.",
            ["Primary Checking", "Savings Vault"],
            ["50% Needs (Rent, Food, Utilities)", "30% Wants (Dining, Leisure)", "20% Savings & Debt Payoff"]
        ),
        new(
            "solopreneur",
            "Solopreneur & Freelance",
            "Built for creators, consultants, and independent founders.",
            "Separates business revenue from personal cash flow, automatically allocates a 30% tax reserve, and builds business runway.",
            ["Personal Checking", "Business Operating Account", "Tax Reserve Vault", "Profit Vault"],
            ["Personal Living Expenses", "Business Software & Hosting", "Tax Withholding (30%)", "Owner Profit Share", "Runway & Buffer"]
        ),
        new(
            "blank",
            "Blank Canvas",
            "Start completely clean and define your own structure.",
            "Initializes one primary account and lets you configure envelopes, categories, and rules from scratch.",
            ["Primary Checking"],
            ["General Expenses"]
        )
    ];

    public static Currency ParseCurrency(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return Currency.Usd;
        return code.Trim().ToUpperInvariant() switch
        {
            "NGN" => Currency.Ngn,
            "USD" => Currency.Usd,
            "GBP" => Currency.Gbp,
            "EUR" => Currency.Eur,
            "USDT" => Currency.Usdt,
            _ => Currency.Usd
        };
    }

    public static async Task ApplyTemplateAsync(
        FinanceDbContext db,
        Owner owner,
        string templateKey,
        Currency currency,
        decimal initialBalance = 0,
        string? primaryAccountName = null,
        CancellationToken ct = default)
    {
        var key = templateKey?.ToLowerInvariant() switch
        {
            "fifty_thirty_twenty" or "50_30_20" or "503020" => "fifty_thirty_twenty",
            "solopreneur" or "freelance" or "business" => "solopreneur",
            "blank" => "blank",
            _ => "zero_based"
        };

        owner.BaseCurrency = currency;
        owner.Archetype = key;
        owner.OnboardingCompleted = true;

        switch (key)
        {
            case "fifty_thirty_twenty":
                await ApplyFiftyThirtyTwenty(db, owner, currency, initialBalance, primaryAccountName, ct);
                break;
            case "solopreneur":
                await ApplySolopreneur(db, owner, currency, initialBalance, primaryAccountName, ct);
                break;
            case "blank":
                await ApplyBlank(db, owner, currency, initialBalance, primaryAccountName, ct);
                break;
            case "zero_based":
            default:
                await ApplyZeroBased(db, owner, currency, initialBalance, primaryAccountName, ct);
                break;
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task ApplyZeroBased(FinanceDbContext db, Owner owner, Currency currency, decimal initialBalance, string? primaryAccountName, CancellationToken ct)
    {
        var checking = new FinancialAccount
        {
            OwnerId = owner.Id,
            Slug = "primary-checking",
            Name = string.IsNullOrWhiteSpace(primaryAccountName) ? "Primary Checking" : primaryAccountName.Trim(),
            Institution = "Primary Bank",
            Role = AccountRole.DailySpending,
            Currency = currency,
            AllowsDailySpending = true,
            Job = "Income deposit, bills payment, and day-to-day card spending.",
            DoNotPutHere = "Emergency fund or long-term investments.",
            WhenToUse = "Daily debits and monthly bill payments.",
            OperatingRule = "Keep at least 1 month of living expenses buffered."
        };

        var emergencyVault = new FinancialAccount
        {
            OwnerId = owner.Id,
            Slug = "emergency-vault",
            Name = "Emergency Vault",
            Institution = "High-Yield Vault",
            Role = AccountRole.SavingsVault,
            Currency = currency,
            AllowsDailySpending = false,
            Job = "Uncompromised emergency fund for 3-6 months of expenses.",
            DoNotPutHere = "Daily groceries or impulse spending.",
            WhenToUse = "True medical, job loss, or catastrophic emergencies only."
        };

        var sinkingVault = new FinancialAccount
        {
            OwnerId = owner.Id,
            Slug = "sinking-fund-vault",
            Name = "Sinking Fund Vault",
            Institution = "Savings Bank",
            Role = AccountRole.StrategicBuffer,
            Currency = currency,
            AllowsDailySpending = false,
            Job = "Irregular expenses, car insurance, annual subscriptions, and travel.",
            DoNotPutHere = "Everyday discretionary coffee or dining."
        };

        db.Accounts.AddRange(checking, emergencyVault, sinkingVault);

        // Envelopes
        var envHousing = new Envelope
        {
            OwnerId = owner.Id,
            Slug = "housing",
            Name = "Housing & Rent",
            Class = EnvelopeClass.Committed,
            IsEssential = true,
            Purpose = "Monthly rent, mortgage, or maintenance.",
            UseRule = "Fully fund before the 1st of each month."
        };
        var envGroceries = new Envelope
        {
            OwnerId = owner.Id,
            Slug = "groceries",
            Name = "Groceries & Nutrition",
            Class = EnvelopeClass.Spend,
            IsEssential = true,
            Purpose = "Weekly food, supermarkets, and essential household items.",
            UseRule = "Fund weekly or per payday."
        };
        var envUtilities = new Envelope
        {
            OwnerId = owner.Id,
            Slug = "utilities",
            Name = "Utilities & Bills",
            Class = EnvelopeClass.Committed,
            IsEssential = true,
            Purpose = "Electricity, water, gas, home internet, phone bill."
        };
        var envTransit = new Envelope
        {
            OwnerId = owner.Id,
            Slug = "transportation",
            Name = "Transportation & Transit",
            Class = EnvelopeClass.Spend,
            IsEssential = true,
            Purpose = "Fuel, public transit, subway, uber, vehicle maintenance."
        };
        var envDiscretionary = new Envelope
        {
            OwnerId = owner.Id,
            Slug = "discretionary",
            Name = "Discretionary & Dining",
            Class = EnvelopeClass.Spend,
            IsEssential = false,
            Purpose = "Restaurants, entertainment, coffee, hobbies."
        };
        var envEmergency = new Envelope
        {
            OwnerId = owner.Id,
            Slug = "emergency-reserve",
            Name = "Emergency Vault Reserve",
            Class = EnvelopeClass.Emergency,
            IsEssential = true,
            Purpose = "Untouchable emergency buffer."
        };
        var envBuffer = new Envelope
        {
            OwnerId = owner.Id,
            Slug = "sinking-buffer",
            Name = "Sinking Fund & Buffer",
            Class = EnvelopeClass.Buffer,
            IsEssential = false,
            Purpose = "Annual bills and unexpected one-off maintenance."
        };

        db.Envelopes.AddRange(envHousing, envGroceries, envUtilities, envTransit, envDiscretionary, envEmergency, envBuffer);

        // Categories
        db.Categories.AddRange(
            new Category { OwnerId = owner.Id, Slug = "housing", Name = "Housing & Rent", EnvelopeId = envHousing.Id },
            new Category { OwnerId = owner.Id, Slug = "groceries", Name = "Groceries", EnvelopeId = envGroceries.Id },
            new Category { OwnerId = owner.Id, Slug = "utilities", Name = "Utilities", EnvelopeId = envUtilities.Id },
            new Category { OwnerId = owner.Id, Slug = "transportation", Name = "Transportation", EnvelopeId = envTransit.Id },
            new Category { OwnerId = owner.Id, Slug = "dining", Name = "Dining Out & Drinks", EnvelopeId = envDiscretionary.Id },
            new Category { OwnerId = owner.Id, Slug = "income", Name = "Salary & Income" }
        );

        // If initial balance > 0, record snapshot on checking
        if (initialBalance > 0)
        {
            var minor = Money.FromMajor(initialBalance, currency).MinorUnits;
            db.BalanceSnapshots.Add(new BalanceSnapshot
            {
                AccountId = checking.Id,
                AmountMinor = minor,
                Currency = currency,
                AsOf = DateOnly.FromDateTime(DateTime.UtcNow),
                Provenance = Provenance.Confirmed,
                Source = "onboarding",
                Notes = "Initial balance set during onboarding."
            });
        }
    }

    private static async Task ApplyFiftyThirtyTwenty(FinanceDbContext db, Owner owner, Currency currency, decimal initialBalance, string? primaryAccountName, CancellationToken ct)
    {
        var checking = new FinancialAccount
        {
            OwnerId = owner.Id,
            Slug = "primary-checking",
            Name = string.IsNullOrWhiteSpace(primaryAccountName) ? "Primary Checking" : primaryAccountName.Trim(),
            Institution = "Primary Bank",
            Role = AccountRole.DailySpending,
            Currency = currency,
            AllowsDailySpending = true,
            Job = "Covers monthly 50% Needs and 30% Wants."
        };

        var savings = new FinancialAccount
        {
            OwnerId = owner.Id,
            Slug = "savings-vault",
            Name = "Savings & Wealth Vault",
            Institution = "Savings Bank",
            Role = AccountRole.SavingsVault,
            Currency = currency,
            AllowsDailySpending = false,
            Job = "Holds 20% savings, emergency fund, and investment capital."
        };

        db.Accounts.AddRange(checking, savings);

        var envNeeds = new Envelope
        {
            OwnerId = owner.Id,
            Slug = "needs-50",
            Name = "Needs (50%)",
            Class = EnvelopeClass.Committed,
            IsEssential = true,
            Purpose = "Rent, groceries, utilities, transit, insurance."
        };
        var envWants = new Envelope
        {
            OwnerId = owner.Id,
            Slug = "wants-30",
            Name = "Wants (30%)",
            Class = EnvelopeClass.Spend,
            IsEssential = false,
            Purpose = "Dining out, shopping, hobbies, streaming, travel."
        };
        var envSavings = new Envelope
        {
            OwnerId = owner.Id,
            Slug = "savings-20",
            Name = "Savings & Goals (20%)",
            Class = EnvelopeClass.Savings,
            IsEssential = true,
            Purpose = "Emergency fund, debt payoff, retirement, index funds."
        };

        db.Envelopes.AddRange(envNeeds, envWants, envSavings);

        db.Categories.AddRange(
            new Category { OwnerId = owner.Id, Slug = "needs", Name = "50% Needs", EnvelopeId = envNeeds.Id },
            new Category { OwnerId = owner.Id, Slug = "wants", Name = "30% Wants", EnvelopeId = envWants.Id },
            new Category { OwnerId = owner.Id, Slug = "savings", Name = "20% Savings", EnvelopeId = envSavings.Id },
            new Category { OwnerId = owner.Id, Slug = "income", Name = "Primary Income" }
        );

        if (initialBalance > 0)
        {
            var minor = Money.FromMajor(initialBalance, currency).MinorUnits;
            db.BalanceSnapshots.Add(new BalanceSnapshot
            {
                AccountId = checking.Id,
                AmountMinor = minor,
                Currency = currency,
                AsOf = DateOnly.FromDateTime(DateTime.UtcNow),
                Provenance = Provenance.Confirmed,
                Source = "onboarding",
                Notes = "Initial balance set during onboarding."
            });
        }

        await Task.CompletedTask;
    }

    private static async Task ApplySolopreneur(FinanceDbContext db, Owner owner, Currency currency, decimal initialBalance, string? primaryAccountName, CancellationToken ct)
    {
        var personalChecking = new FinancialAccount
        {
            OwnerId = owner.Id,
            Slug = "personal-checking",
            Name = "Personal Checking",
            Institution = "Personal Bank",
            Role = AccountRole.DailySpending,
            Currency = currency,
            AllowsDailySpending = true,
            Job = "Personal household expenses and salary draws."
        };

        var bizChecking = new FinancialAccount
        {
            OwnerId = owner.Id,
            Slug = "business-operating",
            Name = string.IsNullOrWhiteSpace(primaryAccountName) ? "Business Operating" : primaryAccountName.Trim(),
            Institution = "Business Bank",
            Role = AccountRole.DailySpending,
            Currency = currency,
            AllowsDailySpending = true,
            Job = "Client receipts, SaaS invoices, contractor payments."
        };

        var taxVault = new FinancialAccount
        {
            OwnerId = owner.Id,
            Slug = "tax-reserve-vault",
            Name = "Tax Reserve Vault",
            Institution = "Reserve Vault",
            Role = AccountRole.SavingsVault,
            Currency = currency,
            AllowsDailySpending = false,
            Job = "Untouchable 30% tax withholding on all gross revenue."
        };

        var profitVault = new FinancialAccount
        {
            OwnerId = owner.Id,
            Slug = "profit-vault",
            Name = "Profit & Runway Vault",
            Institution = "Reserve Vault",
            Role = AccountRole.SavingsVault,
            Currency = currency,
            AllowsDailySpending = false,
            Job = "Business runway and quarterly profit distributions."
        };

        db.Accounts.AddRange(personalChecking, bizChecking, taxVault, profitVault);

        var envPersonal = new Envelope
        {
            OwnerId = owner.Id,
            Slug = "personal-living",
            Name = "Personal Living & Essentials",
            Class = EnvelopeClass.Spend,
            IsEssential = true,
            Purpose = "Personal rent, groceries, and living."
        };
        var envBizSoftware = new Envelope
        {
            OwnerId = owner.Id,
            Slug = "business-operations",
            Name = "Business Ops & Software",
            Class = EnvelopeClass.Committed,
            IsEssential = true,
            Purpose = "Servers, AI APIs, domain names, accounting tools."
        };
        var envTax = new Envelope
        {
            OwnerId = owner.Id,
            Slug = "tax-reserve",
            Name = "Tax Reserve (30%)",
            Class = EnvelopeClass.Emergency,
            IsEssential = true,
            Purpose = "Quarterly and annual tax liabilities."
        };
        var envProfit = new Envelope
        {
            OwnerId = owner.Id,
            Slug = "owner-profit",
            Name = "Owner Profit Share",
            Class = EnvelopeClass.Savings,
            IsEssential = false,
            Purpose = "Retained earnings and owner dividend distributions."
        };

        db.Envelopes.AddRange(envPersonal, envBizSoftware, envTax, envProfit);

        db.Categories.AddRange(
            new Category { OwnerId = owner.Id, Slug = "business-revenue", Name = "Business Client Revenue", IsBusiness = true },
            new Category { OwnerId = owner.Id, Slug = "business-expense", Name = "Business Operating Expense", EnvelopeId = envBizSoftware.Id, IsBusiness = true },
            new Category { OwnerId = owner.Id, Slug = "personal-expense", Name = "Personal Expenses", EnvelopeId = envPersonal.Id },
            new Category { OwnerId = owner.Id, Slug = "tax-payment", Name = "Tax Remittance", EnvelopeId = envTax.Id, IsBusiness = true }
        );

        // Also add an initial business entity for the solopreneur
        var biz = new Business
        {
            OwnerId = owner.Id,
            Slug = "main-venture",
            Name = "Primary Venture",
            Description = "Primary solopreneur business",
            ReinvestPercent = 70,
            PersonalPercent = 30,
            BaselineCostsJson = "[]"
        };
        db.Businesses.Add(biz);

        if (initialBalance > 0)
        {
            var minor = Money.FromMajor(initialBalance, currency).MinorUnits;
            db.BalanceSnapshots.Add(new BalanceSnapshot
            {
                AccountId = bizChecking.Id,
                AmountMinor = minor,
                Currency = currency,
                AsOf = DateOnly.FromDateTime(DateTime.UtcNow),
                Provenance = Provenance.Confirmed,
                Source = "onboarding",
                Notes = "Initial business operating balance."
            });
        }

        await Task.CompletedTask;
    }

    private static async Task ApplyBlank(FinanceDbContext db, Owner owner, Currency currency, decimal initialBalance, string? primaryAccountName, CancellationToken ct)
    {
        var checking = new FinancialAccount
        {
            OwnerId = owner.Id,
            Slug = "primary-account",
            Name = string.IsNullOrWhiteSpace(primaryAccountName) ? "Primary Account" : primaryAccountName.Trim(),
            Institution = "Bank",
            Role = AccountRole.DailySpending,
            Currency = currency,
            AllowsDailySpending = true,
            Job = "General day-to-day finances."
        };

        var env = new Envelope
        {
            OwnerId = owner.Id,
            Slug = "general-expenses",
            Name = "General Expenses",
            Class = EnvelopeClass.Spend,
            IsEssential = true,
            Purpose = "General living expenses."
        };

        db.Accounts.Add(checking);
        db.Envelopes.Add(env);

        db.Categories.AddRange(
            new Category { OwnerId = owner.Id, Slug = "general", Name = "General Expense", EnvelopeId = env.Id },
            new Category { OwnerId = owner.Id, Slug = "income", Name = "Income" }
        );

        if (initialBalance > 0)
        {
            var minor = Money.FromMajor(initialBalance, currency).MinorUnits;
            db.BalanceSnapshots.Add(new BalanceSnapshot
            {
                AccountId = checking.Id,
                AmountMinor = minor,
                Currency = currency,
                AsOf = DateOnly.FromDateTime(DateTime.UtcNow),
                Provenance = Provenance.Confirmed,
                Source = "onboarding",
                Notes = "Initial balance set during onboarding."
            });
        }

        await Task.CompletedTask;
    }
}
