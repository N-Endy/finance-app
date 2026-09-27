using FinanceOS.Domain;
using FinanceOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceOS.Infrastructure.Data;

public static class PlanSeeder
{
    public const string SnapshotName = "Nnamdi_Comprehensive_Financial_Plan_v2";
    public static readonly DateOnly CaptureDate = new(2026, 9, 1);

    public static async Task SeedAsync(FinanceDbContext db, Owner owner, CancellationToken ct = default)
    {
        if (owner.PlanSeeded) return;

        var accounts = SeedAccounts(owner.Id);
        var envelopes = SeedEnvelopes(owner.Id, accounts);
        var categories = SeedCategories(owner.Id, envelopes);
        var goals = SeedGoals(owner.Id, accounts);
        var income = SeedIncome(owner.Id, accounts);
        var plans = SeedPlans(owner.Id, income, envelopes, accounts, goals);
        var family = SeedFamily(owner.Id);
        var holdings = SeedHoldings(owner.Id, accounts);
        var business = new Business
        {
            OwnerId = owner.Id,
            Slug = "matchpredictor",
            Name = "MatchPredictor",
            ReinvestPercent = 70,
            PersonalPercent = 30
        };
        var rules = SeedRules(owner.Id);
        var notes = SeedNotes(owner.Id);
        var recurring = SeedRecurring(owner.Id, accounts, envelopes);
        var subscriptions = SeedSubscriptions(owner.Id, accounts);

        db.Accounts.AddRange(accounts.Values);
        db.Envelopes.AddRange(envelopes.Values);
        db.Categories.AddRange(categories.Values);
        db.Goals.AddRange(goals.Values);
        db.IncomeSources.AddRange(income.Values);
        db.AllocationPlans.AddRange(plans);
        db.FamilyRecipients.AddRange(family);
        db.Holdings.AddRange(holdings);
        db.Businesses.Add(business);
        db.Rules.AddRange(rules);
        db.ExternalNotes.AddRange(notes);
        db.RecurringItems.AddRange(recurring);
        db.Subscriptions.AddRange(subscriptions);
        db.Pensions.Add(new PensionAccount { OwnerId = owner.Id, Provenance = Provenance.Unknown });
        db.RetirementAssumptions.Add(new RetirementAssumption { OwnerId = owner.Id });

        SeedSnapshots(db, accounts);
        SeedLedger(db, owner.Id, accounts, categories, envelopes, business.Id);
        SeedExampleAssignments(db, accounts, envelopes);

        owner.PlanSeeded = true;
        owner.HomeSavingsLockNote = "Workbook recorded a lock until Oct 8 without a year. Confirm the date before any lock warning depends on it.";
        await db.SaveChangesAsync(ct);
    }

    private static Dictionary<string, FinancialAccount> SeedAccounts(Guid ownerId)
    {
        FinancialAccount A(string slug, string name, string institution, AccountRole role, string job,
            string avoid, string when, string rule, long? floor, long? ceiling, bool spend, bool netWorth = true) =>
            new()
            {
                OwnerId = ownerId,
                Slug = slug,
                Name = name,
                Institution = institution,
                Role = role,
                Job = job,
                DoNotPutHere = avoid,
                WhenToUse = when,
                OperatingRule = rule,
                FloorMinor = floor,
                CeilingMinor = ceiling,
                AllowsDailySpending = spend,
                IncludeInNetWorth = netWorth,
                Currency = Currency.Ngn
            };

        return new Dictionary<string, FinancialAccount>
        {
            ["stanbic"] = A("stanbic", "Stanbic", "Stanbic IBTC", AccountRole.Clearing,
                "Salary command centre and immediate bills.",
                "Long-term savings, housing, emergency, investments.",
                "Salary arrival and bills.",
                "Clearing account, not a wealth vault. Do not treat the whole balance as spendable.",
                5_000_000, null, false),
            ["kuda"] = A("kuda", "Kuda", "Kuda", AccountRole.StrategicBuffer,
                "Secondary-income holding and strategic buffer.",
                "Lifestyle inflation, betting, long-term wealth parked without a job.",
                "When secondary income arrives.",
                "Do not transfer the ₦400k wholesale to Stanbic.",
                0, 10_000_000, false),
            ["opay"] = A("opay", "OPay", "OPay", AccountRole.DailySpending,
                "Daily spending wallet.",
                "Emergency, investments, housing, children savings, betting bank.",
                "Food, transport, airtime, small purchases.",
                "If it runs low, review the budget before topping up. Starting allowance band ₦60,000–₦80,000.",
                500_000, 8_000_000, true),
            ["cowrywise-emergency"] = A("cowrywise-emergency", "Cowrywise Emergency", "Cowrywise", AccountRole.SavingsVault,
                "True emergency reserve.",
                "Betting, dates, shopping, routine family, moving, investments, lifestyle.",
                "Genuine emergencies only.",
                "Protect. Do not use to finance the move.",
                null, null, false),
            ["cowrywise-mmf"] = A("cowrywise-mmf", "Cowrywise MMF", "Cowrywise", AccountRole.SavingsVault,
                "Medium-term liquid wealth.",
                "Daily spending, betting, casual requests.",
                "Savings and investments.",
                "Review exact fund terms. Do not invent a yield.",
                null, null, false),
            ["cowrywise-children"] = A("cowrywise-children", "Cowrywise Children", "Cowrywise", AccountRole.SavingsVault,
                "Long-term children fund.",
                "Any current spending.",
                "Leave untouched.",
                "Workbook noted a reported ~11.95% p.a. Store as a note only until you confirm the statement.",
                null, null, false),
            ["cowrywise-home"] = A("cowrywise-home", "Cowrywise Home", "Cowrywise", AccountRole.SavingsVault,
                "Existing home savings.",
                "Daily spending.",
                "Housing / move, then re-purpose after moving.",
                "Last known lock note: until Oct 8, year not captured.",
                null, null, false),
            ["cowrywise-stocks"] = A("cowrywise-stocks", "Cowrywise Stocks", "Cowrywise", AccountRole.InvestmentBroker,
                "Long-term Nigerian stocks.",
                "Spending money.",
                "Investments.",
                "Okomu + Presco. Verify current value.",
                null, null, false),
            ["piggyvest-housing"] = A("piggyvest-housing", "PiggyVest Housing", "PiggyVest", AccountRole.GoalVault,
                "Move / housing capital.",
                "Daily spending, emergency, unassigned investments.",
                "Housing and named sinking funds.",
                "Every bucket needs a named purpose. Current balance was not captured.",
                0, 300_000_000, false),
            ["piggyvest-irregular"] = A("piggyvest-irregular", "PiggyVest Annual / Irregular", "PiggyVest", AccountRole.GoalVault,
                "Clothes, gifts, Christmas, travel, annual expenses, tech.",
                "Daily spending.",
                "Planned irregular expenses only.",
                "Accumulate month to month.",
                0, null, false),
            ["access"] = A("access", "Access Bank", "Access Bank", AccountRole.BusinessCard,
                "Business and international subscription card.",
                "Groceries, family, everyday spending, housing.",
                "Cursor, Railway, domain, AI/SaaS, MatchPredictor.",
                "Visa card = business/subscriptions only.",
                null, null, false),
            ["cash"] = A("cash", "Physical Cash", "Cash", AccountRole.CashOnHand,
                "Small daily cash.",
                "Savings.",
                "Transport and tiny purchases.",
                "Last known ₦1,800 on 1 Sep 2026.",
                0, null, true),
            ["bamboo-ng"] = A("bamboo-ng", "Bamboo Nigerian Stocks", "Bamboo", AccountRole.InvestmentBroker,
                "Long-term Nigerian stocks.",
                "Spending money.",
                "Investments.",
                "Approximate. Verify.",
                null, null, false),
            ["bamboo-us"] = A("bamboo-us", "Bamboo US Stocks", "Bamboo", AccountRole.InvestmentBroker,
                "Long-term US stocks.",
                "Spending money.",
                "Investments.",
                "Automated $30/month. Keep in USD until an FX rate is entered.",
                null, null, false),
            ["risevest"] = A("risevest", "Risevest Real Estate", "Risevest", AccountRole.InvestmentBroker,
                "Real estate diversification.",
                "Spending money.",
                "Investments.",
                "Automated $25/month. Keep in USD until an FX rate is entered.",
                null, null, false),
            ["rotating-savings"] = A("rotating-savings", "Rotating Family Savings", "Family scheme", AccountRole.ExternalWallet,
                "Expected housing payout.",
                "Lifestyle.",
                "When the payout is received.",
                "EXPECTED receivable of about ₦1.2m. Excluded from net worth until received. Monthly ₦100k is the same pool, not extra capital.",
                null, null, false, false),
            ["sportybet"] = A("sportybet", "SportyBet wallet", "SportyBet", AccountRole.ExternalWallet,
                "External betting wallet tracked for awareness only.",
                "Any plan money.",
                "Never. Allocation is ₦0.",
                "Partial August data only. Do not infer profit or loss without stakes and winnings.",
                null, null, false, false)
        };
    }

    private static Dictionary<string, Envelope> SeedEnvelopes(Guid ownerId, Dictionary<string, FinancialAccount> accounts)
    {
        Envelope E(string slug, string name, EnvelopeClass cls, string purpose, string rule, string? fund, bool essential = false) =>
            new()
            {
                OwnerId = ownerId,
                Slug = slug,
                Name = name,
                Class = cls,
                Purpose = purpose,
                UseRule = rule,
                DefaultFundingAccountId = fund is null ? null : accounts[fund].Id,
                IsEssential = essential
            };

        return new Dictionary<string, Envelope>
        {
            ["food"] = E("food", "Food", EnvelopeClass.Spend, "Home food and controlled eating out.", "Keep within ₦45,000.", "opay", true),
            ["family"] = E("family", "Family", EnvelopeClass.Committed, "Household feeding, aunt, brother school fees, cushion.", "Do not expand automatically.", "stanbic", true),
            ["data-airtime"] = E("data-airtime", "Data + Airtime", EnvelopeClass.Spend, "₦30k data + ₦5k airtime. Office internet is employer-paid.", "Keep within ₦35,000.", "stanbic", true),
            ["transport"] = E("transport", "Transport", EnvelopeClass.Spend, "Work transport and walking buffer.", "Target monthly ceiling ₦12,000.", "opay", true),
            ["personal-care"] = E("personal-care", "Personal Care", EnvelopeClass.Sinking, "Hair, toiletries, deodorant, perfume.", "Accumulate for larger purchases.", "stanbic"),
            ["dating"] = E("dating", "Dating", EnvelopeClass.Spend, "Relationship / date budget.", "Monthly ceiling ₦30,000.", "opay"),
            ["career-business"] = E("career-business", "Career / Business", EnvelopeClass.Business, "Tools that increase earning power.", "Track MatchPredictor costs on the business ledger as well.", "access"),
            ["irregular-giving"] = E("irregular-giving", "Irregular / Giving", EnvelopeClass.Spend, "Offering, gifts, small extras.", "Use for non-routine requests.", "stanbic"),
            ["emergency"] = E("emergency", "Emergency Fund", EnvelopeClass.Emergency, "Liquidity protection.", "Do not use for moving.", "cowrywise-emergency", true),
            ["mmf"] = E("mmf", "MMF", EnvelopeClass.Investment, "Medium-term liquidity / wealth.", "Review fund terms.", "cowrywise-mmf"),
            ["children"] = E("children", "Children Savings", EnvelopeClass.DoNotTouch, "Long-term locked child fund.", "Leave untouched.", "cowrywise-children"),
            ["rotating"] = E("rotating", "Rotating Savings", EnvelopeClass.Housing, "Family rotating scheme treated as moving capital.", "Do not add monthly ₦100k on top of the expected ₦1.2m payout.", "rotating-savings"),
            ["home"] = E("home", "Home Savings", EnvelopeClass.Housing, "Existing Cowrywise home fund.", "Verify current balance.", "cowrywise-home"),
            ["operating-reserve"] = E("operating-reserve", "Operating Reserve", EnvelopeClass.Buffer, "Timing, charges, small surprises.", "Build toward ₦100k floor, then redirect.", "stanbic"),
            ["housing-move"] = E("housing-move", "Housing / Move", EnvelopeClass.Housing, "Own 1-bedroom move capital.", "Never use the emergency fund.", "piggyvest-housing"),
            ["annual-irregular"] = E("annual-irregular", "Annual / Irregular Fund", EnvelopeClass.Sinking, "Clothes, gifts, Christmas, travel, annual subscriptions, tech.", "Use only for planned irregular expenses.", "piggyvest-irregular"),
            ["next-rent"] = E("next-rent", "Next Rent", EnvelopeClass.Sinking, "Annual rent renewal after moving.", "Begin immediately after moving. Target ₦125k/month toward ₦1.5m.", "piggyvest-housing"),
            ["betting"] = E("betting", "Betting", EnvelopeClass.Spend, "Awareness only. Plan allocation is ₦0.", "Do not treat as entertainment. Warn on any activity.", "opay")
        };
    }

    private static Dictionary<string, Category> SeedCategories(Guid ownerId, Dictionary<string, Envelope> envelopes)
    {
        Category C(string slug, string name, string? parent, string? env, bool betting = false, bool family = false, bool business = false, bool transfer = false) =>
            new()
            {
                OwnerId = ownerId,
                Slug = slug,
                Name = name,
                ParentSlug = parent,
                EnvelopeId = env is null ? null : envelopes[env].Id,
                IsBetting = betting,
                IsFamily = family,
                IsBusiness = business,
                IsTransfer = transfer
            };

        return new Dictionary<string, Category>
        {
            ["salary"] = C("salary", "Salary", null, null),
            ["secondary-income"] = C("secondary-income", "Secondary income", null, null),
            ["food"] = C("food", "Food", null, "food"),
            ["groceries"] = C("groceries", "Groceries", "food", "food"),
            ["family"] = C("family", "Family", null, "family", family: true),
            ["data-airtime"] = C("data-airtime", "Data + Airtime", null, "data-airtime"),
            ["airtime"] = C("airtime", "Airtime", "data-airtime", "data-airtime"),
            ["transport"] = C("transport", "Transport", null, "transport"),
            ["personal-care"] = C("personal-care", "Personal Care", null, "personal-care"),
            ["dating"] = C("dating", "Dating", null, "dating"),
            ["career-business"] = C("career-business", "Career / Business", null, "career-business"),
            ["software"] = C("software", "Software", "career-business", "career-business"),
            ["irregular-giving"] = C("irregular-giving", "Irregular / Giving", null, "irregular-giving"),
            ["emergency"] = C("emergency", "Emergency Fund", null, "emergency"),
            ["mmf"] = C("mmf", "MMF", null, "mmf"),
            ["children"] = C("children", "Children Savings", null, "children"),
            ["rotating"] = C("rotating", "Rotating Savings", null, "rotating"),
            ["home"] = C("home", "Home Savings", null, "home"),
            ["operating-reserve"] = C("operating-reserve", "Operating Reserve", null, "operating-reserve"),
            ["housing"] = C("housing", "Housing", null, "housing-move"),
            ["betting"] = C("betting", "Betting", null, "betting", betting: true),
            ["digital"] = C("digital", "Digital", null, "dating"),
            ["debt"] = C("debt", "Debt", null, null),
            ["bank-charges"] = C("bank-charges", "Bank charges", null, "operating-reserve"),
            ["cash"] = C("cash", "Cash", null, null),
            ["transfer"] = C("transfer", "Transfer", null, null, transfer: true),
            ["hosting"] = C("hosting", "Hosting", null, "career-business", business: true),
            ["database"] = C("database", "Database", null, "career-business", business: true),
            ["ai"] = C("ai", "AI / API", null, "career-business", business: true),
            ["domain"] = C("domain", "Domain", null, "career-business", business: true),
            ["revenue"] = C("revenue", "Business revenue", null, null, business: true)
        };
    }

    private static Dictionary<string, Goal> SeedGoals(Guid ownerId, Dictionary<string, FinancialAccount> accounts) =>
        new()
        {
            ["housing"] = new Goal { OwnerId = ownerId, Slug = "housing", Name = "Move / housing capital", Kind = GoalKind.Housing, TargetMinor = 300_000_000, MonthlyContributionMinor = 25_000_000, FundingAccountId = accounts["piggyvest-housing"].Id, Rule = "Build without the emergency fund. Rotating payout is expected separately and must not be double-counted." },
            ["emergency"] = new Goal { OwnerId = ownerId, Slug = "emergency", Name = "Emergency fund", Kind = GoalKind.Emergency, TargetMinor = 240_000_000, MonthlyContributionMinor = 5_000_000, FundingAccountId = accounts["cowrywise-emergency"].Id, Rule = "Final target becomes 6 × actual essential post-move expenses once those exist." },
            ["next-rent"] = new Goal { OwnerId = ownerId, Slug = "next-rent", Name = "Next annual rent", Kind = GoalKind.NextRent, TargetMinor = 150_000_000, MonthlyContributionMinor = 12_500_000, FundingAccountId = accounts["piggyvest-housing"].Id, Rule = "Begin immediately after moving. Not funded yet." },
            ["children"] = new Goal { OwnerId = ownerId, Slug = "children", Name = "Children savings", Kind = GoalKind.Children, TargetMinor = 0, MonthlyContributionMinor = 2_000_000, FundingAccountId = accounts["cowrywise-children"].Id, Rule = "Leave untouched. No fabricated end value." },
            ["retirement"] = new Goal { OwnerId = ownerId, Slug = "retirement", Name = "Retirement / long-term wealth", Kind = GoalKind.Retirement, TargetMinor = 50_000_000_000, MonthlyContributionMinor = 0, IsAspiration = true, Rule = "₦500m+ is an aspiration, not a guaranteed-return calculation." },
            ["operating-reserve"] = new Goal { OwnerId = ownerId, Slug = "operating-reserve", Name = "Operating reserve", Kind = GoalKind.OperatingReserve, TargetMinor = 10_000_000, MonthlyContributionMinor = 2_600_000, FundingAccountId = accounts["stanbic"].Id, Rule = "Build toward ₦100k floor." },
            ["annual-irregular"] = new Goal { OwnerId = ownerId, Slug = "annual-irregular", Name = "Annual / irregular fund", Kind = GoalKind.SinkingFund, TargetMinor = 36_000_000, MonthlyContributionMinor = 3_000_000, FundingAccountId = accounts["piggyvest-irregular"].Id, Rule = "Use only for planned irregular expenses." }
        };

    private static Dictionary<string, IncomeSource> SeedIncome(Guid ownerId, Dictionary<string, FinancialAccount> accounts) =>
        new()
        {
            ["salary"] = new IncomeSource
            {
                OwnerId = ownerId, Slug = "salary", Name = "Main salary", ExpectedAmountMinor = 54_300_000,
                ExpectedDayOfMonth = 27, Reliability = IncomeReliability.Reliable,
                DestinationAccountId = accounts["stanbic"].Id,
                Rule = "Lifestyle runs on this income."
            },
            ["secondary"] = new IncomeSource
            {
                OwnerId = ownerId, Slug = "secondary", Name = "Secondary income", ExpectedAmountMinor = 40_000_000,
                ExpectedDayOfMonth = 12, Reliability = IncomeReliability.Strategic,
                DestinationAccountId = accounts["kuda"].Id,
                Rule = "Strategic and uncertain. Do not build lifestyle dependence. Do not transfer wholesale to Stanbic."
            }
        };

    private static List<AllocationPlan> SeedPlans(Guid ownerId, Dictionary<string, IncomeSource> income,
        Dictionary<string, Envelope> envelopes, Dictionary<string, FinancialAccount> accounts, Dictionary<string, Goal> goals)
    {
        AllocationLine L(int order, string name, AllocationKind kind, long? amount, string env, string dest, string? goal, string rule, string? charge = null) =>
            new()
            {
                SortOrder = order,
                Name = name,
                Kind = kind,
                AmountMinor = amount,
                EnvelopeId = envelopes[env].Id,
                DestinationAccountId = accounts[dest].Id,
                GoalId = goal is null ? null : goals[goal].Id,
                Rule = rule,
                ActualChargeKey = charge
            };

        var salary = new AllocationPlan
        {
            OwnerId = ownerId,
            IncomeSourceId = income["salary"].Id,
            Name = "Salary ₦543,000",
            Lines =
            [
                L(1, "Food", AllocationKind.Fixed, 4_500_000, "food", "opay", null, "Keep within ₦45k"),
                L(2, "Family", AllocationKind.Fixed, 8_500_000, "family", "stanbic", null, "Do not expand automatically"),
                L(3, "Data + Airtime", AllocationKind.Fixed, 3_500_000, "data-airtime", "stanbic", null, "Office internet is employer-paid"),
                L(4, "Transport", AllocationKind.Fixed, 1_200_000, "transport", "opay", null, "Target monthly ceiling"),
                L(5, "Personal Care", AllocationKind.Fixed, 2_000_000, "personal-care", "stanbic", null, "Accumulate for larger purchases"),
                L(6, "Dating", AllocationKind.Fixed, 3_000_000, "dating", "opay", null, "Monthly ceiling"),
                L(7, "Career / Business", AllocationKind.Fixed, 4_500_000, "career-business", "access", null, "Prioritize earning power"),
                L(8, "Irregular / Giving", AllocationKind.Fixed, 2_000_000, "irregular-giving", "stanbic", null, "Non-routine requests"),
                L(9, "Emergency Fund", AllocationKind.Fixed, 5_000_000, "emergency", "cowrywise-emergency", "emergency", "Do not use for moving"),
                L(10, "MMF", AllocationKind.Fixed, 5_000_000, "mmf", "cowrywise-mmf", null, "Review fund terms"),
                L(11, "Children Savings", AllocationKind.Fixed, 2_000_000, "children", "cowrywise-children", "children", "Leave untouched"),
                L(12, "Rotating Savings", AllocationKind.Fixed, 10_000_000, "rotating", "rotating-savings", "housing", "Treat as moving capital"),
                L(13, "Home Savings", AllocationKind.Fixed, 500_000, "home", "cowrywise-home", "housing", "Current known balance ₦37,336"),
                L(14, "Operating Reserve", AllocationKind.Fixed, 2_600_000, "operating-reserve", "stanbic", "operating-reserve", "Build toward ₦100k floor")
            ]
        };

        var secondary = new AllocationPlan
        {
            OwnerId = ownerId,
            IncomeSourceId = income["secondary"].Id,
            Name = "Secondary ₦400,000 waterfall",
            Lines =
            [
                L(1, "Risevest $25", AllocationKind.ActualCharge, null, "mmf", "risevest", null, "Use actual naira charge.", "risevest-25"),
                L(2, "Bamboo $30", AllocationKind.ActualCharge, null, "mmf", "bamboo-us", null, "Use actual naira charge.", "bamboo-30"),
                L(3, "Moving Fund", AllocationKind.Fixed, 25_000_000, "housing-move", "piggyvest-housing", "housing", "Do not use emergency fund"),
                L(4, "Annual / Irregular", AllocationKind.Fixed, 3_000_000, "annual-irregular", "piggyvest-irregular", "annual-irregular", "Do not spend until needed"),
                L(5, "Additional wealth", AllocationKind.Fixed, 3_000_000, "emergency", "cowrywise-emergency", "emergency", "Additional protection"),
                L(6, "Kuda remainder", AllocationKind.Remainder, null, "operating-reserve", "kuda", null, "Strategic buffer, not lifestyle")
            ]
        };

        return [salary, secondary];
    }

    private static List<FamilyRecipient> SeedFamily(Guid ownerId) =>
    [
        new() { OwnerId = ownerId, Name = "Household feeding", Purpose = "General household feeding", RecurringAmountMinor = 2_500_000, IsRecurring = true },
        new() { OwnerId = ownerId, Name = "Aunt", Purpose = "Aunt upkeep", RecurringAmountMinor = 3_000_000, IsRecurring = true },
        new() { OwnerId = ownerId, Name = "Brother school fees", Purpose = "Brother's school-fee sinking fund", RecurringAmountMinor = 2_666_700, IsRecurring = true },
        new() { OwnerId = ownerId, Name = "Family cushion", Purpose = "Small family buffer", RecurringAmountMinor = 333_300, IsRecurring = true },
        new() { OwnerId = ownerId, Name = "Cousin", Purpose = "One-off support", IsRecurring = false },
        new() { OwnerId = ownerId, Name = "Brother", Purpose = "One-off support", IsRecurring = false }
    ];

    private static List<Holding> SeedHoldings(Guid ownerId, Dictionary<string, FinancialAccount> accounts) =>
    [
        H(ownerId, "emergency", "Cowrywise Emergency Fund", accounts["cowrywise-emergency"].Id, 153_748_800, Currency.Ngn, "Liquid/low-risk", "6-month emergency reserve", "Protect; verify current balance", false),
        H(ownerId, "mmf", "Cowrywise MMF", accounts["cowrywise-mmf"].Id, 232_521_200, Currency.Ngn, "Liquid/low-risk", "Medium-term liquidity / wealth", "Review exact fund terms", false),
        H(ownerId, "children", "Children Savings", accounts["cowrywise-children"].Id, 10_692_200, Currency.Ngn, "Locked 18 years", "Long-term children goal", "Reported ~11.95% p.a. is a note, not a projection input.", false),
        H(ownerId, "cw-stocks", "Cowrywise Stocks", accounts["cowrywise-stocks"].Id, 32_691_000, Currency.Ngn, "Market", "Long-term investment", "Okomu + Presco; approximate", false),
        H(ownerId, "bamboo-ng", "Bamboo Nigerian Stocks", accounts["bamboo-ng"].Id, 59_500_000, Currency.Ngn, "Market", "Long-term investment", "Approximate; verify", false),
        H(ownerId, "bamboo-us", "Bamboo US Stocks", accounts["bamboo-us"].Id, 60_000, Currency.Usd, "Market", "Long-term investment", "Keep in USD until you enter an FX rate.", false),
        H(ownerId, "risevest", "Risevest Real Estate", accounts["risevest"].Id, 15_700, Currency.Usd, "Illiquid/longer-term", "Real estate diversification", "Keep in USD until you enter an FX rate.", false),
        H(ownerId, "home", "Cowrywise Home Savings", accounts["cowrywise-home"].Id, 3_733_600, Currency.Ngn, "Locked until Oct 8 (year not captured)", "Housing / move", "Verify current status", false),
        H(ownerId, "rotating", "Rotating Family Savings", accounts["rotating-savings"].Id, 120_000_000, Currency.Ngn, "Expected payout", "Housing / move", "Expected payout, not an investment.", true)
    ];

    private static Holding H(Guid ownerId, string slug, string name, Guid accountId, long amount, Currency ccy,
        string liquidity, string purpose, string note, bool expected) =>
        new()
        {
            OwnerId = ownerId,
            Slug = slug,
            Name = name,
            AccountId = accountId,
            AmountMinor = amount,
            Currency = ccy,
            Provenance = expected ? Provenance.Expected : Provenance.LastKnown,
            AsOf = CaptureDate,
            Liquidity = liquidity,
            Purpose = purpose,
            StatusNote = note,
            IsExpectedReceivable = expected,
            IncludeInNetWorth = !expected,
            MonthlyContributionMinor = 0
        };

    private static List<FinancialRule> SeedRules(Guid ownerId) =>
    [
        R(ownerId, "income", "Income rule", "Lifestyle runs on ₦543k salary.", "₦400k secondary income is strategic/uncertain."),
        R(ownerId, "housing", "Housing rule", "Move only when dedicated housing capital is sufficient.", "Do not raid the emergency fund."),
        R(ownerId, "emergency", "Emergency rule", "Protect the emergency fund.", "Use only for genuine emergencies."),
        R(ownerId, "betting", "Betting rule", "Current budget = ₦0.", "Use paper/simulated testing for MatchPredictor."),
        R(ownerId, "family", "Family rule", "Use the family allocation and irregular/giving fund.", "Do not automatically increase support every time cash is available."),
        R(ownerId, "investment", "Investment rule", "Keep long-term money invested according to its purpose.", "Do not sell investments for routine spending."),
        R(ownerId, "career", "Career rule", "Career/business tools should increase earning power.", "Track Railway and MatchPredictor separately."),
        R(ownerId, "rent", "Rent rule", "Start next rent sinking fund immediately after moving.", "At ₦1.5m annual rent, target ₦125k/month."),
        R(ownerId, "income-rise", "Income-rise rule", "Lifestyle rises slower than income.", "Send most increases to wealth/housing/retirement."),
        R(ownerId, "ledger", "Ledger rule", "Record every transaction.", "Transfers are not spending."),
        R(ownerId, "reconciliation", "Reconciliation rule", "Compare ledger to actual account balances.", "Mark UNRECONCILED instead of guessing."),
        R(ownerId, "review", "Monthly review", "Review income, spending, savings, investments, behaviour and net worth.", "Adjust next month based on actuals.")
    ];

    private static FinancialRule R(Guid ownerId, string code, string title, string action, string control) =>
        new() { OwnerId = ownerId, Code = code, Title = title, Action = action, Control = control };

    private static List<ExternalNote> SeedNotes(Guid ownerId) =>
    [
        new()
        {
            OwnerId = ownerId,
            Topic = "sportybet-august",
            AsOf = new DateOnly(2026, 8, 31),
            Provenance = Provenance.LastKnown,
            Body = "August deposits ₦48,787; withdrawals ₦21,000; wallet screenshot ₦2,039.19. This does not match the single ₦11,000 SportyBet ledger line. Both facts are kept. Net betting result is UNKNOWN without stakes and winnings."
        },
        new()
        {
            OwnerId = ownerId,
            Topic = "matchpredictor-placeholders",
            AsOf = CaptureDate,
            Provenance = Provenance.Plan,
            Body = "Railway hosting, domain (~$15/year), AI tokens (~$5) and AdSense are listed in the workbook at ₦0 or as estimates. Neon DB ₦15,000 is a known plan estimate, labelled ESTIMATE until an actual charge is entered. No revenue has been recorded."
        },
        new()
        {
            OwnerId = ownerId,
            Topic = "unknown-balances",
            AsOf = CaptureDate,
            Provenance = Provenance.Unknown,
            Body = "Stanbic after 1 Sep, PiggyVest, Access Bank, RSA/pension, and any FX rate were not supplied. Enter them before using those figures."
        }
    ];

    private static List<RecurringItem> SeedRecurring(Guid ownerId, Dictionary<string, FinancialAccount> accounts, Dictionary<string, Envelope> envelopes) =>
    [
        new() { OwnerId = ownerId, Name = "Salary", DayOfMonth = 27, AmountMinor = 54_300_000, AccountId = accounts["stanbic"].Id, Kind = "income" },
        new() { OwnerId = ownerId, Name = "Secondary income", DayOfMonth = 12, AmountMinor = 40_000_000, AccountId = accounts["kuda"].Id, Kind = "income" },
        new() { OwnerId = ownerId, Name = "Emergency contribution", DayOfMonth = 27, AmountMinor = 5_000_000, AccountId = accounts["cowrywise-emergency"].Id, EnvelopeId = envelopes["emergency"].Id, Kind = "savings" },
        new() { OwnerId = ownerId, Name = "MMF contribution", DayOfMonth = 27, AmountMinor = 5_000_000, AccountId = accounts["cowrywise-mmf"].Id, EnvelopeId = envelopes["mmf"].Id, Kind = "savings" },
        new() { OwnerId = ownerId, Name = "Children savings", DayOfMonth = 27, AmountMinor = 2_000_000, AccountId = accounts["cowrywise-children"].Id, EnvelopeId = envelopes["children"].Id, Kind = "savings" },
        new() { OwnerId = ownerId, Name = "Rotating savings", DayOfMonth = 27, AmountMinor = 10_000_000, AccountId = accounts["rotating-savings"].Id, EnvelopeId = envelopes["rotating"].Id, Kind = "savings" },
        new() { OwnerId = ownerId, Name = "Home savings", DayOfMonth = 27, AmountMinor = 500_000, AccountId = accounts["cowrywise-home"].Id, EnvelopeId = envelopes["home"].Id, Kind = "savings" },
        new() { OwnerId = ownerId, Name = "Housing from secondary income", DayOfMonth = 12, AmountMinor = 25_000_000, AccountId = accounts["piggyvest-housing"].Id, EnvelopeId = envelopes["housing-move"].Id, Kind = "savings" }
    ];

    private static List<Subscription> SeedSubscriptions(Guid ownerId, Dictionary<string, FinancialAccount> accounts) =>
    [
        new() { OwnerId = ownerId, Name = "Cursor", AmountMinor = 2_800_000, Currency = Currency.Ngn, Frequency = SubscriptionFrequency.Monthly, PaymentAccountId = accounts["access"].Id, NextBillingDate = new DateOnly(2026, 10, 27), Category = "Software", IsBusiness = true, IsActive = true },
        new() { OwnerId = ownerId, Name = "Railway", AmountMinor = 0, Currency = Currency.Ngn, Frequency = SubscriptionFrequency.Monthly, PaymentAccountId = accounts["access"].Id, NextBillingDate = new DateOnly(2026, 10, 1), Category = "Hosting", IsBusiness = true, IsActive = true },
        new() { OwnerId = ownerId, Name = "Neon DB", AmountMinor = 1_500_000, Currency = Currency.Ngn, Frequency = SubscriptionFrequency.Monthly, PaymentAccountId = accounts["access"].Id, NextBillingDate = new DateOnly(2026, 10, 1), Category = "Database", IsBusiness = true, IsActive = true },
        new() { OwnerId = ownerId, Name = "Domain", AmountMinor = 0, Currency = Currency.Usd, Frequency = SubscriptionFrequency.Yearly, PaymentAccountId = accounts["access"].Id, NextBillingDate = new DateOnly(2026, 12, 1), Category = "Domain", IsBusiness = true, IsActive = true }
    ];

    private static void SeedSnapshots(FinanceDbContext db, Dictionary<string, FinancialAccount> accounts)
    {
        void Snap(string slug, long amount, Currency ccy, Provenance p, string? notes = null) =>
            db.BalanceSnapshots.Add(new BalanceSnapshot
            {
                AccountId = accounts[slug].Id,
                AmountMinor = amount,
                Currency = ccy,
                AsOf = CaptureDate,
                Provenance = p,
                Source = SnapshotName,
                Notes = notes
            });

        Snap("kuda", 0, Currency.Ngn, Provenance.LastKnown, "Last confirmed ₦0.");
        Snap("opay", 0, Currency.Ngn, Provenance.LastKnown, "Last confirmed ₦0 after the brother transfer.");
        Snap("cash", 180_000, Currency.Ngn, Provenance.LastKnown, "Last known 1 Sep 2026.");
        Snap("cowrywise-emergency", 153_748_800, Currency.Ngn, Provenance.LastKnown, "Verify current.");
        Snap("cowrywise-mmf", 232_521_200, Currency.Ngn, Provenance.LastKnown, "Verify current.");
        Snap("cowrywise-children", 10_692_200, Currency.Ngn, Provenance.LastKnown, "Verify current.");
        Snap("cowrywise-home", 3_733_600, Currency.Ngn, Provenance.LastKnown, "Verify current.");
        Snap("cowrywise-stocks", 32_691_000, Currency.Ngn, Provenance.LastKnown, "Approximate.");
        Snap("bamboo-ng", 59_500_000, Currency.Ngn, Provenance.LastKnown, "Approximate.");
        Snap("bamboo-us", 60_000, Currency.Usd, Provenance.LastKnown, "USD. No FX conversion.");
        Snap("risevest", 15_700, Currency.Usd, Provenance.LastKnown, "USD. No FX conversion.");
        Snap("sportybet", 203_919, Currency.Ngn, Provenance.LastKnown, "Wallet screenshot ₦2,039.19. Partial.");
    }

    private static void SeedExampleAssignments(FinanceDbContext db, Dictionary<string, FinancialAccount> accounts, Dictionary<string, Envelope> envelopes)
    {
        // Spec example lives as documentation assignments on a virtual illustration only if Stanbic has a confirmed ₦180k.
        // We do not invent a Stanbic balance. No assignments are seeded for Stanbic.
        _ = accounts;
        _ = envelopes;
        _ = db;
    }

    private static void SeedLedger(
        FinanceDbContext db,
        Guid ownerId,
        Dictionary<string, FinancialAccount> accounts,
        Dictionary<string, Category> categories,
        Dictionary<string, Envelope> envelopes,
        Guid businessId)
    {
        void Tx(
            DateOnly date, string account, TransactionType type, string? category, string description,
            long inflow, long outflow, long fee, bool transfer, string? notes, string? other = null,
            bool business = false, string? env = null)
        {
            var amount = inflow > 0 ? inflow : outflow;
            var signedForAccount = inflow > 0 ? inflow : -outflow;
            var tx = new LedgerTransaction
            {
                OwnerId = ownerId,
                Date = date,
                Type = type,
                AccountId = accounts[account].Id,
                CounterpartyAccountId = other is null ? null : accounts[other].Id,
                AmountMinor = amount,
                FeeMinor = fee,
                Currency = Currency.Ngn,
                CategoryId = category is null ? null : categories[category].Id,
                EnvelopeId = env is null ? null : envelopes[env].Id,
                BusinessId = business ? businessId : null,
                Description = description,
                Notes = notes,
                IsBusiness = business,
                IsTransfer = transfer
            };

            tx.Postings.Add(new Posting
            {
                AccountId = accounts[account].Id,
                AmountMinor = signedForAccount,
                Currency = Currency.Ngn,
                Role = "primary"
            });

            if (fee > 0)
            {
                tx.Postings.Add(new Posting
                {
                    AccountId = accounts[account].Id,
                    AmountMinor = -fee,
                    Currency = Currency.Ngn,
                    Role = "fee"
                });
            }

            if (transfer && other is not null && inflow == 0)
            {
                tx.Postings.Add(new Posting
                {
                    AccountId = accounts[other].Id,
                    AmountMinor = amount,
                    Currency = Currency.Ngn,
                    Role = "counterparty"
                });
            }

            db.Transactions.Add(tx);
        }

        Tx(new(2026, 8, 26), "stanbic", TransactionType.Income, "salary", "Main salary", 54_300_000, 0, 0, false, "Salary cycle");
        Tx(new(2026, 8, 26), "stanbic", TransactionType.Transfer, "transfer", "Transfer to OPay", 0, 20_000_000, 10_375, true, "₦50 transfer fee + ₦3.75 + ₦50 stamp duty", "opay");
        Tx(new(2026, 8, 26), "opay", TransactionType.Transfer, "transfer", "Received from Stanbic", 20_000_000, 0, 0, true, "Counter-entry", "stanbic");
        Tx(new(2026, 8, 26), "opay", TransactionType.Debt, "debt", "OPay loan repayment", 0, 5_672_000, 0, false, "Loan repaid");
        Tx(new(2026, 8, 26), "opay", TransactionType.Fee, "bank-charges", "Loan stamp duty", 0, 5_000, 0, false, null);
        Tx(new(2026, 8, 26), "opay", TransactionType.Expense, "betting", "SportyBet funding", 0, 1_100_000, 0, false, "Plan currently allocates ₦0 to betting", env: "betting");
        Tx(new(2026, 8, 27), "opay", TransactionType.Expense, "food", "Lunch", 0, 430_000, 0, false, null, env: "food");
        Tx(new(2026, 8, 27), "opay", TransactionType.Expense, "digital", "Video-call app coins", 0, 250_400, 0, false, null);
        Tx(new(2026, 8, 27), "opay", TransactionType.Savings, "rotating", "Rotating contribution", 0, 10_000_000, 0, false, null, "rotating-savings", env: "rotating");
        Tx(new(2026, 8, 27), "opay", TransactionType.Withdrawal, "cash", "Cash withdrawal", 0, 500_000, 10_000, false, "₦100 withdrawal fee", "cash");
        Tx(new(2026, 8, 27), "stanbic", TransactionType.Debt, "debt", "Church Bazaar debt", 0, 5_007_688, 0, false, "₦50k + ₦25 + ₦1.88 + ₦50 stamp duty");
        Tx(new(2026, 8, 27), "stanbic", TransactionType.Expense, "data-airtime", "Monthly data", 0, 3_000_000, 0, false, null, env: "data-airtime");
        Tx(new(2026, 8, 27), "stanbic", TransactionType.Expense, "software", "Cursor", 0, 2_800_000, 0, false, "Actual recent charge", env: "career-business");
        Tx(new(2026, 8, 27), "opay", TransactionType.Expense, "family", "Cousin support", 0, 100_000, 0, false, "One-off. Not a recurring obligation.", env: "family");
        Tx(new(2026, 8, 28), "opay", TransactionType.Expense, "airtime", "MTN recharge", 0, 200_000, 0, false, null, env: "data-airtime");
        Tx(new(2026, 8, 28), "opay", TransactionType.Expense, "food", "Breakfast", 0, 192_000, 0, false, null, env: "food");
        Tx(new(2026, 9, 1), "opay", TransactionType.Expense, "food", "Lunch", 0, 390_000, 0, false, null, env: "food");
        Tx(new(2026, 9, 1), "opay", TransactionType.Expense, "family", "Brother support", 0, 1_509_000, 0, false, "Remaining OPay balance sent to brother. One-off.", env: "family");
        Tx(new(2026, 9, 1), "cash", TransactionType.Expense, "transport", "Transport", 0, 50_000, 0, false, null, env: "transport");
        Tx(new(2026, 8, 27), "cash", TransactionType.Deposit, "cash", "Received from OPay", 500_000, 0, 0, true, "Counter-entry", "opay");
        Tx(new(2026, 8, 27), "cash", TransactionType.Expense, "transport", "Transport", 0, 150_000, 0, false, null, env: "transport");
        Tx(new(2026, 8, 27), "cash", TransactionType.Expense, "groceries", "Sugar", 0, 60_000, 0, false, null, env: "food");
        Tx(new(2026, 8, 27), "cash", TransactionType.Expense, "groceries", "Milk", 0, 60_000, 0, false, null, env: "food");
        Tx(new(2026, 8, 27), "cowrywise-home", TransactionType.Savings, "home", "Automated home savings", 0, 500_000, 0, false, "Workbook recorded this as an outflow on Cowrywise Home. Verify whether the contribution actually arrived from another account. Sign was not silently flipped.", env: "home");
    }
}
