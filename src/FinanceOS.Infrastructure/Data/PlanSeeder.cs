using FinanceOS.Domain;
using FinanceOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceOS.Infrastructure.Data;

public static class PlanSeeder
{
    public const string SnapshotName = "Nnamdi_Comprehensive_Financial_Plan_v2_EmptyLedger";
    public const string SnapshotTopic = "plan-snapshot";
    public static readonly DateOnly CaptureDate = new(2026, 9, 26);

    public static async Task EnsureLatestSnapshotAsync(FinanceDbContext db, CancellationToken ct = default)
    {
        var owners = await db.Owners.ToListAsync(ct);
        foreach (var owner in owners)
        {
            var note = await db.ExternalNotes.FirstOrDefaultAsync(n => n.OwnerId == owner.Id && n.Topic == SnapshotTopic, ct);
            if (note?.Body == SnapshotName) continue;
            if (owner.PlanSeeded || note is not null)
            {
                await ClearOwnerPlanAsync(db, owner, ct);
                owner.PlanSeeded = false;
            }

            await SeedAsync(db, owner, ct);
        }
    }

    public static async Task ClearOwnerPlanAsync(FinanceDbContext db, Owner owner, CancellationToken ct = default)
    {
        var txs = await db.Transactions.Include(t => t.Postings).Include(t => t.Revisions)
            .Where(t => t.OwnerId == owner.Id).ToListAsync(ct);
        db.RemoveRange(txs);

        var accounts = await db.Accounts.Include(a => a.Snapshots).Include(a => a.Assignments)
            .Where(a => a.OwnerId == owner.Id).ToListAsync(ct);
        foreach (var account in accounts)
        {
            account.Snapshots.Clear();
            account.Assignments.Clear();
        }

        var plans = await db.AllocationPlans.Include(p => p.Lines)
            .Where(p => p.OwnerId == owner.Id).ToListAsync(ct);
        db.RemoveRange(plans);

        db.RemoveRange(await db.FamilySupports.Where(f => f.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.FamilyRecipients.Where(f => f.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.Holdings.Where(h => h.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.Goals.Where(g => g.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.Categories.Where(c => c.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.Envelopes.Where(e => e.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.IncomeSources.Where(i => i.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.RecurringItems.Where(r => r.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.Subscriptions.Where(s => s.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.Rules.Where(r => r.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.Violations.Where(v => v.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.Actions.Where(a => a.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.ExternalNotes.Where(n => n.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.ActualCharges.Where(c => c.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.Businesses.Where(b => b.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.Liabilities.Where(l => l.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.CounterpartyLoans.Where(l => l.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.FixedAssets.Where(f => f.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.ExchangeRates.Where(r => r.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.Pensions.Where(p => p.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(await db.RetirementAssumptions.Where(r => r.OwnerId == owner.Id).ToListAsync(ct));
        db.RemoveRange(accounts);
        await db.SaveChangesAsync(ct);
    }

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
        db.Pensions.Add(new PensionAccount
        {
            OwnerId = owner.Id,
            CurrentBalanceMinor = null,
            Provenance = Provenance.Unknown
        });
        db.RetirementAssumptions.Add(new RetirementAssumption { OwnerId = owner.Id });

        SeedExampleAssignments(db, accounts, envelopes);

        owner.PlanSeeded = true;
        owner.HomeSavingsLockNote = null;
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
                "Salary and bills.",
                "Long-term savings, housing, emergency, investments.",
                "Salary arrival and bills.",
                "Clearing account. The balance is not spendable cash.",
                5_000_000, null, false),
            ["kuda"] = A("kuda", "Kuda", "Kuda", AccountRole.StrategicBuffer,
                "Secondary income holding.",
                "Daily spending, betting.",
                "When secondary income arrives.",
                "Keep the ₦400k waterfall here.",
                0, 10_000_000, false),
            ["opay"] = A("opay", "OPay", "OPay", AccountRole.DailySpending,
                "Daily spending.",
                "Emergency, investments, housing, children savings.",
                "Food, transport, airtime, small purchases.",
                "Allowance band ₦60,000–₦80,000.",
                500_000, 8_000_000, true),
            ["cowrywise-emergency"] = A("cowrywise-emergency", "Cowrywise Emergency", "Cowrywise", AccountRole.SavingsVault,
                "Emergency reserve.",
                "Moving, routine spending, investments.",
                "Genuine emergencies.",
                "Not for the move.",
                null, null, false),
            ["cowrywise-mmf"] = A("cowrywise-mmf", "Cowrywise MMF", "Cowrywise", AccountRole.SavingsVault,
                "Medium-term liquid wealth.",
                "Daily spending.",
                "Savings and investments.",
                "Yield is unknown until the statement is confirmed.",
                null, null, false),
            ["cowrywise-children"] = A("cowrywise-children", "Cowrywise Children", "Cowrywise", AccountRole.SavingsVault,
                "Children fund.",
                "Current spending.",
                "Leave untouched.",
                "Locked long-term.",
                null, null, false),
            ["cowrywise-home"] = A("cowrywise-home", "Cowrywise Home", "Cowrywise", AccountRole.SavingsVault,
                "Home savings.",
                "Daily spending.",
                "Housing / move.",
                "Balance is unknown until entered.",
                null, null, false),
            ["cowrywise-stocks"] = A("cowrywise-stocks", "Cowrywise Stocks", "Cowrywise", AccountRole.InvestmentBroker,
                "Nigerian stocks.",
                "Spending.",
                "Investments.",
                "Okomu + Presco.",
                null, null, false),
            ["piggyvest-housing"] = A("piggyvest-housing", "PiggyVest Housing", "PiggyVest", AccountRole.GoalVault,
                "Housing capital.",
                "Daily spending, emergency.",
                "Housing and sinking funds.",
                "Balance is unknown until entered.",
                0, 300_000_000, false),
            ["piggyvest-irregular"] = A("piggyvest-irregular", "PiggyVest Annual / Irregular", "PiggyVest", AccountRole.GoalVault,
                "Annual and irregular expenses.",
                "Daily spending.",
                "Planned irregular expenses.",
                "Clothes, gifts, Christmas, travel, tech.",
                0, null, false),
            ["access"] = A("access", "Access Bank", "Access Bank", AccountRole.BusinessCard,
                "Business and subscriptions.",
                "Groceries, family, everyday spending.",
                "Cursor, Railway, domain, AI, MatchPredictor.",
                "Balance is unknown until entered.",
                null, null, false),
            ["cash"] = A("cash", "Physical Cash", "Cash", AccountRole.CashOnHand,
                "Small daily cash.",
                "Savings.",
                "Transport and small purchases.",
                "Balance is unknown until entered.",
                0, null, true),
            ["bamboo-ng"] = A("bamboo-ng", "Bamboo Nigerian Stocks", "Bamboo", AccountRole.InvestmentBroker,
                "Nigerian stocks.",
                "Spending.",
                "Investments.",
                "Balance is unknown until entered.",
                null, null, false),
            ["bamboo-us"] = A("bamboo-us", "Bamboo US Stocks", "Bamboo", AccountRole.InvestmentBroker,
                "US stocks.",
                "Spending.",
                "Investments.",
                "$30/month. Held in USD until an FX rate is entered.",
                null, null, false),
            ["risevest"] = A("risevest", "Risevest Real Estate", "Risevest", AccountRole.InvestmentBroker,
                "Real estate holding.",
                "Spending.",
                "Investments.",
                "$25/month. Held in USD until an FX rate is entered.",
                null, null, false),
            ["rotating-savings"] = A("rotating-savings", "Rotating Family Savings", "Family scheme", AccountRole.ExternalWallet,
                "Expected housing payout.",
                "Lifestyle.",
                "When the payout is received.",
                "Expected ₦1.2m. The ₦100k/month is the same pool.",
                null, null, false, false),
            ["sportybet"] = A("sportybet", "SportyBet wallet", "SportyBet", AccountRole.ExternalWallet,
                "Betting wallet, awareness only.",
                "Plan money.",
                "Allocation is ₦0.",
                "Balance is unknown until entered. Profit or loss is unknown.",
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
                L(13, "Home Savings", AllocationKind.Fixed, 500_000, "home", "cowrywise-home", "housing", "Enter the current home savings figure"),
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
        H(ownerId, "emergency", "Cowrywise Emergency Fund", accounts["cowrywise-emergency"].Id, Currency.Ngn, "Liquid/low-risk", "6-month emergency reserve", false),
        H(ownerId, "mmf", "Cowrywise MMF", accounts["cowrywise-mmf"].Id, Currency.Ngn, "Liquid/low-risk", "Medium-term liquidity / wealth", false),
        H(ownerId, "children", "Children Savings", accounts["cowrywise-children"].Id, Currency.Ngn, "Locked 18 years", "Long-term children goal", false),
        H(ownerId, "cw-stocks", "Cowrywise Stocks", accounts["cowrywise-stocks"].Id, Currency.Ngn, "Market", "Long-term investment", false),
        H(ownerId, "bamboo-ng", "Bamboo Nigerian Stocks", accounts["bamboo-ng"].Id, Currency.Ngn, "Market", "Long-term investment", false),
        H(ownerId, "bamboo-us", "Bamboo US Stocks", accounts["bamboo-us"].Id, Currency.Usd, "Market", "Long-term investment", false),
        H(ownerId, "risevest", "Risevest Real Estate", accounts["risevest"].Id, Currency.Usd, "Illiquid/longer-term", "Real estate diversification", false),
        H(ownerId, "home", "Cowrywise Home Savings", accounts["cowrywise-home"].Id, Currency.Ngn, "Housing vault", "Housing / move", false),
        H(ownerId, "rotating", "Rotating Family Savings", accounts["rotating-savings"].Id, Currency.Ngn, "Expected payout", "Housing / move", true)
    ];

    private static Holding H(Guid ownerId, string slug, string name, Guid accountId, Currency ccy,
        string liquidity, string purpose, bool expected) =>
        new()
        {
            OwnerId = ownerId,
            Slug = slug,
            Name = name,
            AccountId = accountId,
            AmountMinor = 0,
            Currency = ccy,
            Provenance = Provenance.Unknown,
            AsOf = CaptureDate,
            Liquidity = liquidity,
            Purpose = purpose,
            StatusNote = "Enter the current figure.",
            IsExpectedReceivable = expected,
            IncludeInNetWorth = !expected,
            MonthlyContributionMinor = 0
        };

    private static List<FinancialRule> SeedRules(Guid ownerId) =>
    [
        R(ownerId, "income", "Income", "Lifestyle uses the ₦543k salary.", "The ₦400k secondary income stays on its own waterfall."),
        R(ownerId, "housing", "Housing", "Move from dedicated housing capital.", "The emergency fund stays out of the move."),
        R(ownerId, "emergency", "Emergency", "Use only for genuine emergencies.", "The target is provisional until post-move essentials are known."),
        R(ownerId, "betting", "Betting", "Budget is ₦0.", "MatchPredictor is tested on paper."),
        R(ownerId, "family", "Family", "Use the family allocation and irregular/giving fund.", "One-off support stays one-off."),
        R(ownerId, "investment", "Investments", "Long-term money stays invested for its purpose.", "Routine spending does not come from investments."),
        R(ownerId, "career", "Career", "Career tools are tracked on the business ledger.", "Railway and MatchPredictor stay separate from personal spend."),
        R(ownerId, "rent", "Rent", "Next rent starts after moving.", "Target ₦125k/month toward ₦1.5m."),
        R(ownerId, "income-rise", "Income rise", "Lifestyle rises slower than income.", "Most increases go to wealth, housing, or retirement."),
        R(ownerId, "ledger", "Ledger", "Every transaction is recorded.", "Transfers are not spending."),
        R(ownerId, "reconciliation", "Reconciliation", "Compare the ledger to the account balance.", "A mismatch stays UNRECONCILED."),
        R(ownerId, "review", "Monthly review", "Review income, spending, savings, and net worth.", "Next month uses the actuals.")
    ];

    private static FinancialRule R(Guid ownerId, string code, string title, string action, string control) =>
        new() { OwnerId = ownerId, Code = code, Title = title, Action = action, Control = control };

    private static List<ExternalNote> SeedNotes(Guid ownerId) =>
    [
        new()
        {
            OwnerId = ownerId,
            Topic = "sportybet",
            AsOf = CaptureDate,
            Provenance = Provenance.Unknown,
            Body = "SportyBet wallet and net betting result are unknown until you enter them. Plan allocation is ₦0."
        },
        new()
        {
            OwnerId = ownerId,
            Topic = "matchpredictor-placeholders",
            AsOf = CaptureDate,
            Provenance = Provenance.Plan,
            Body = "Railway ₦16,000, Neon ₦15,000, and AI ₦14,000 are plan estimates until an actual charge is entered. Domain is ~$15/year. No revenue has been recorded."
        },
        new()
        {
            OwnerId = ownerId,
            Topic = "unknown-balances",
            AsOf = CaptureDate,
            Provenance = Provenance.Unknown,
            Body = "Every account balance, holding, pension figure, and FX rate is unknown until entered."
        },
        new()
        {
            OwnerId = ownerId,
            Topic = SnapshotTopic,
            AsOf = CaptureDate,
            Provenance = Provenance.Plan,
            Body = SnapshotName
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
        new() { OwnerId = ownerId, Name = "Railway", AmountMinor = 1_600_000, Currency = Currency.Ngn, Frequency = SubscriptionFrequency.Monthly, PaymentAccountId = accounts["access"].Id, NextBillingDate = new DateOnly(2026, 10, 1), Category = "Hosting", IsBusiness = true, IsActive = true },
        new() { OwnerId = ownerId, Name = "Neon DB", AmountMinor = 1_500_000, Currency = Currency.Ngn, Frequency = SubscriptionFrequency.Monthly, PaymentAccountId = accounts["access"].Id, NextBillingDate = new DateOnly(2026, 10, 1), Category = "Database", IsBusiness = true, IsActive = true },
        new() { OwnerId = ownerId, Name = "AI tokens", AmountMinor = 1_400_000, Currency = Currency.Ngn, Frequency = SubscriptionFrequency.Monthly, PaymentAccountId = accounts["access"].Id, NextBillingDate = new DateOnly(2026, 10, 1), Category = "AI", IsBusiness = true, IsActive = true },
        new() { OwnerId = ownerId, Name = "Domain", AmountMinor = 0, Currency = Currency.Usd, Frequency = SubscriptionFrequency.Yearly, PaymentAccountId = accounts["access"].Id, NextBillingDate = new DateOnly(2026, 12, 1), Category = "Domain", IsBusiness = true, IsActive = true }
    ];

    private static void SeedExampleAssignments(FinanceDbContext db, Dictionary<string, FinancialAccount> accounts, Dictionary<string, Envelope> envelopes)
    {
        // No envelope assignments until a confirmed account balance exists.
        _ = accounts;
        _ = envelopes;
        _ = db;
    }
}
