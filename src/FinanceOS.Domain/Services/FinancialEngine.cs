using FinanceOS.Domain.Entities;

namespace FinanceOS.Domain.Services;

public sealed record ExplainLine(
    string Label,
    long? AmountMinor,
    Currency Currency,
    Provenance Provenance,
    string? Note = null)
{
    public bool IsUnknown => AmountMinor is null || Provenance == Provenance.Unknown;
}

public sealed record Explanation(
    string Metric,
    string Sentence,
    string Formula,
    IReadOnlyList<ExplainLine> Lines,
    long? ResultMinor,
    Currency Currency,
    Provenance ResultProvenance)
{
    public bool IsUnknown => ResultMinor is null || ResultProvenance == Provenance.Unknown;
}

public sealed record ReconciliationResult(
    Guid AccountId,
    string AccountName,
    long? OpeningMinor,
    long InflowsMinor,
    long OutflowsMinor,
    long FeesMinor,
    long AdjustmentsMinor,
    long? ExpectedClosingMinor,
    long? ActualMinor,
    long? DifferenceMinor,
    ReconciliationStatus Status,
    DateOnly? ActualAsOf,
    Provenance? ActualProvenance,
    string Sentence);

public sealed record SpendableResult(
    long SpendableMinor,
    Currency Currency,
    IReadOnlyList<ExplainLine> Lines,
    string Sentence);

public static class AllocationMath
{
    public static long SumFixedLines(IEnumerable<long> amounts) => amounts.Sum();

    public static void EnsureSalaryBalances(long incomeMinor, long allocatedMinor)
    {
        if (allocatedMinor > incomeMinor)
        {
            throw new DomainException(
                $"RED ALERT. Allocations {Money.FromMajor(allocatedMinor / 100m, Currency.Ngn)} exceed income {Money.FromMajor(incomeMinor / 100m, Currency.Ngn)}.");
        }
    }

    public static long? Unallocated(long incomeMinor, long allocatedMinor)
    {
        if (allocatedMinor > incomeMinor) return null;
        return incomeMinor - allocatedMinor;
    }
}

public static class SpendableCalculator
{
    public static SpendableResult FromAssignments(
        IReadOnlyList<(string AccountName, AccountRole Role, bool AllowsDailySpending, string EnvelopeName, EnvelopeClass Class, long AmountMinor, Currency Currency)> assignments,
        Currency currency)
    {
        var spendable = 0L;
        var lines = new List<ExplainLine>();

        foreach (var row in assignments.Where(a => a.Currency == currency))
        {
            var counts = row.AllowsDailySpending && row.Class == EnvelopeClass.Spend;
            if (counts)
            {
                spendable += row.AmountMinor;
            }

            lines.Add(new ExplainLine(
                $"{row.AccountName} → {row.EnvelopeName}",
                row.AmountMinor,
                currency,
                Provenance.Confirmed,
                counts
                    ? "Counts toward actually spendable because it sits in a daily-spending account and has a spend job."
                    : "Has a job. Not free cash."));
        }

        var sentence = spendable == 0
            ? "Spendable is ₦0."
            : $"You can spend {Money.FromMajor(spendable / 100m, currency)} from unused daily-spending envelopes.";

        return new SpendableResult(spendable, currency, lines, sentence);
    }
}

public static class ReconciliationCalculator
{
    public static ReconciliationResult Calculate(
        Guid accountId,
        string accountName,
        long? openingMinor,
        long inflowsMinor,
        long outflowsMinor,
        long feesMinor,
        long adjustmentsMinor,
        long? actualMinor,
        DateOnly? actualAsOf,
        Provenance? actualProvenance)
    {
        long? expected = openingMinor is null
            ? null
            : openingMinor.Value + inflowsMinor - outflowsMinor - feesMinor + adjustmentsMinor;

        long? difference = expected is null || actualMinor is null
            ? null
            : actualMinor.Value - expected.Value;

        ReconciliationStatus status;
        string sentence;

        if (openingMinor is null || actualMinor is null)
        {
            status = ReconciliationStatus.Incomplete;
            var missing = new List<string>();
            if (openingMinor is null) missing.Add("an opening balance");
            if (actualMinor is null) missing.Add("a current actual balance");
            sentence = $"{accountName} is incomplete. Enter {string.Join(" and ", missing)} before this account can be reconciled. The ledger is not being guessed.";
        }
        else if (difference == 0)
        {
            status = ReconciliationStatus.Reconciled;
            sentence = $"{accountName} matches the ledger.";
        }
        else
        {
            status = ReconciliationStatus.Unreconciled;
            sentence =
                $"{accountName} is UNRECONCILED. Expected {FormatNgn(expected!.Value)}, actual {FormatNgn(actualMinor!.Value)}, difference {FormatNgn(difference!.Value)}. No explanation has been invented. Investigate the gap.";
        }

        return new ReconciliationResult(
            accountId,
            accountName,
            openingMinor,
            inflowsMinor,
            outflowsMinor,
            feesMinor,
            adjustmentsMinor,
            expected,
            actualMinor,
            difference,
            status,
            actualAsOf,
            actualProvenance,
            sentence);
    }

    private static string FormatNgn(long minor) => Money.FromMajor(minor / 100m, Currency.Ngn).Format();
}

public static class NetWorthCalculator
{
    public static Explanation Calculate(
        IReadOnlyList<(string Name, long AmountMinor, Currency Currency, Provenance Provenance, bool Include, bool IsExpectedReceivable, bool IsLiability)> items,
        ExchangeRate? usdToNgn)
    {
        var lines = new List<ExplainLine>();
        var confirmedNgn = 0L;
        var hasConfirmedNgn = false;
        var excluded = new List<string>();

        foreach (var item in items)
        {
            if (item.IsExpectedReceivable)
            {
                lines.Add(new ExplainLine(item.Name, item.AmountMinor, item.Currency, Provenance.Expected,
                    "Expected receivable. Excluded from net worth until received."));
                excluded.Add($"{item.Name} is EXPECTED, not an asset yet.");
                continue;
            }

            if (!item.Include)
            {
                lines.Add(new ExplainLine(item.Name, item.AmountMinor, item.Currency, item.Provenance, "Excluded by account rule."));
                continue;
            }

            if (item.Currency == Currency.Usd)
            {
                if (usdToNgn is null)
                {
                    lines.Add(new ExplainLine(item.Name, item.AmountMinor, Currency.Usd, item.Provenance,
                        "USD is shown in original currency. No naira conversion because no FX rate has been entered."));
                    excluded.Add($"{item.Name} is USD and has no entered FX rate.");
                    continue;
                }

                if (item.Provenance != Provenance.Confirmed)
                {
                    lines.Add(new ExplainLine(item.Name, item.AmountMinor, Currency.Usd, item.Provenance,
                        $"Last-known USD. Rate {usdToNgn.Rate} on {usdToNgn.AsOf:yyyy-MM-dd} is available but this holding is not confirmed, so it stays out of confirmed net worth."));
                    excluded.Add($"{item.Name} is {item.Provenance}, not confirmed.");
                    continue;
                }

                var converted = (long)decimal.Round(item.AmountMinor * usdToNgn.Rate, 0, MidpointRounding.AwayFromZero);
                confirmedNgn += item.IsLiability ? -converted : converted;
                hasConfirmedNgn = true;
                lines.Add(new ExplainLine(item.Name, converted, Currency.Ngn, Provenance.Confirmed,
                    $"Original {Money.FromMajor(item.AmountMinor / 100m, Currency.Usd)} × {usdToNgn.Rate} on {usdToNgn.AsOf:yyyy-MM-dd} (source: {usdToNgn.Source})."));
                continue;
            }

            if (item.Provenance != Provenance.Confirmed)
            {
                lines.Add(new ExplainLine(item.Name, item.AmountMinor, item.Currency, item.Provenance,
                    "Not confirmed. Listed for context. Not added to confirmed net worth."));
                excluded.Add($"{item.Name} is {item.Provenance}.");
                continue;
            }

            confirmedNgn += item.IsLiability ? -item.AmountMinor : item.AmountMinor;
            hasConfirmedNgn = true;
            lines.Add(new ExplainLine(item.Name, item.AmountMinor, Currency.Ngn, Provenance.Confirmed, null));
        }

        string sentence;
        Provenance provenance;
        long? result = hasConfirmedNgn ? confirmedNgn : null;

        if (!hasConfirmedNgn)
        {
            provenance = Provenance.Unknown;
            sentence = excluded.Count == 0
                ? "Confirmed net worth is UNKNOWN. No confirmed cash, savings, investments, or liabilities have been entered."
                : "Confirmed net worth is UNKNOWN. Last-known and expected figures are listed separately and have not been treated as today's fact. " + string.Join(" ", excluded);
        }
        else
        {
            provenance = Provenance.Confirmed;
            sentence = excluded.Count == 0
                ? $"Confirmed net worth is {Money.FromMajor(confirmedNgn / 100m, Currency.Ngn)}."
                : $"Confirmed net worth is {Money.FromMajor(confirmedNgn / 100m, Currency.Ngn)}. Excluded from this total: {string.Join(" ", excluded)}";
        }

        return new Explanation(
            "net-worth",
            sentence,
            "Confirmed cash + confirmed savings + confirmed investments + confirmed business assets − confirmed liabilities. Transfers between own accounts are ignored. Expected receivables and last-known figures are excluded.",
            lines,
            result,
            Currency.Ngn,
            provenance);
    }
}

public static class HousingProjection
{
    public sealed record Result(
        long? ConfirmedHousingMinor,
        long? LastKnownHomeSavingsMinor,
        long FutureSecondaryHousingMinor,
        long FutureHomeContributionMinor,
        long? ExpectedRotatingPayoutMinor,
        DateOnly? ProjectedMoveDate,
        bool ProjectedDateIsEstimate,
        string Sentence,
        IReadOnlyList<ExplainLine> Lines);

    public static Result Project(
        long? confirmedPiggyVestMinor,
        long? lastKnownHomeSavingsMinor,
        int remainingContributionMonths,
        long secondaryHousingMonthlyMinor,
        long homeContributionMonthlyMinor,
        long? expectedRotatingPayoutMinor,
        long targetMinor,
        DateOnly today)
    {
        var lines = new List<ExplainLine>
        {
            new("Confirmed PiggyVest / housing vault", confirmedPiggyVestMinor, Currency.Ngn,
                confirmedPiggyVestMinor is null ? Provenance.Unknown : Provenance.Confirmed,
                confirmedPiggyVestMinor is null ? "UNKNOWN. Enter the current PiggyVest housing balance." : null),
            new("Cowrywise Home Savings", lastKnownHomeSavingsMinor, Currency.Ngn,
                lastKnownHomeSavingsMinor is null ? Provenance.Unknown : Provenance.LastKnown,
                lastKnownHomeSavingsMinor is null ? "UNKNOWN. Enter the current home savings figure." : "Verify before treating this as move capital."),
            new("Future secondary-income housing contributions",
                secondaryHousingMonthlyMinor * remainingContributionMonths, Currency.Ngn, Provenance.Plan,
                $"{Money.FromMajor(secondaryHousingMonthlyMinor / 100m, Currency.Ngn)} × {remainingContributionMonths} months. Not double-counted with the rotating payout."),
            new("Future Home Savings contributions",
                homeContributionMonthlyMinor * remainingContributionMonths, Currency.Ngn, Provenance.Plan, null),
            new("Expected rotating-savings payout", expectedRotatingPayoutMinor, Currency.Ngn,
                expectedRotatingPayoutMinor is null ? Provenance.Unknown : Provenance.Expected,
                expectedRotatingPayoutMinor is null
                    ? "UNKNOWN. Enter the expected payout when you know it. Same pool as the ₦100,000 monthly rotating contribution."
                    : "This is the same pool as the ₦100,000 monthly rotating contribution. The monthly ₦100,000 is not added on top of this payout.")
        };

        if (confirmedPiggyVestMinor is null)
        {
            return new Result(
                null,
                lastKnownHomeSavingsMinor,
                secondaryHousingMonthlyMinor * remainingContributionMonths,
                homeContributionMonthlyMinor * remainingContributionMonths,
                expectedRotatingPayoutMinor,
                null,
                false,
                "Projected move date is UNKNOWN until the PiggyVest housing balance is entered.",
                lines);
        }

        var homeNow = lastKnownHomeSavingsMinor ?? 0;
        var rotating = expectedRotatingPayoutMinor ?? 0;
        var knownNow = confirmedPiggyVestMinor.Value + homeNow;
        var monthly = secondaryHousingMonthlyMinor + homeContributionMonthlyMinor;
        var remaining = targetMinor - knownNow - rotating;
        DateOnly? date = null;
        var estimate = false;
        string sentence;

        if (lastKnownHomeSavingsMinor is null || expectedRotatingPayoutMinor is null)
        {
            sentence = "Projected move date stays UNKNOWN until home savings and the expected rotating payout are entered. Confirmed PiggyVest is recorded; missing figures are not invented.";
        }
        else if (remaining <= 0)
        {
            sentence = "Housing capital plus the expected rotating payout covers the ₦3,000,000 target.";
        }
        else if (monthly <= 0)
        {
            sentence = $"You still need {Money.FromMajor(remaining / 100m, Currency.Ngn)} and there is no monthly housing contribution recorded.";
        }
        else
        {
            var months = (int)Math.Ceiling(remaining / (decimal)monthly);
            date = today.AddMonths(months);
            estimate = true;
            sentence =
                $"ESTIMATE: at the planned housing contributions, the remaining {Money.FromMajor(remaining / 100m, Currency.Ngn)} takes about {months} month(s), around {date:MMMM yyyy}. This uses entered home savings and an EXPECTED rotating payout. It is not a promise.";
        }

        return new Result(
            confirmedPiggyVestMinor,
            lastKnownHomeSavingsMinor,
            secondaryHousingMonthlyMinor * remainingContributionMonths,
            homeContributionMonthlyMinor * remainingContributionMonths,
            expectedRotatingPayoutMinor,
            date,
            estimate,
            sentence,
            lines);
    }
}

public static class SecondaryWaterfall
{
    public sealed record Line(string Name, long? AmountMinor, Provenance Provenance, string Note);

    public sealed record Result(IReadOnlyList<Line> Lines, long? RemainderMinor, string Sentence);

    public static Result Apply(long incomeMinor, long? risevestNgnMinor, long? bambooNgnMinor)
    {
        var lines = new List<Line>
        {
            new("Risevest $25 actual naira charge", risevestNgnMinor,
                risevestNgnMinor is null ? Provenance.Unknown : Provenance.Confirmed,
                risevestNgnMinor is null ? "Enter the actual naira charge. Do not invent an FX rate." : "Actual charge."),
            new("Bamboo $30 actual naira charge", bambooNgnMinor,
                bambooNgnMinor is null ? Provenance.Unknown : Provenance.Confirmed,
                bambooNgnMinor is null ? "Enter the actual naira charge. Do not invent an FX rate." : "Actual charge."),
            new("Housing / PiggyVest", 25_000_000, Provenance.Plan, "₦250,000 while preparing to move."),
            new("Annual / irregular", 3_000_000, Provenance.Plan, "₦30,000 sinking fund."),
            new("Additional wealth / emergency", 3_000_000, Provenance.Plan, "₦30,000.")
        };

        if (risevestNgnMinor is null || bambooNgnMinor is null)
        {
            return new Result(lines, null,
                "Kuda remainder is UNKNOWN until the actual naira charges for Risevest $25 and Bamboo $30 are recorded. The remainder is not forced to a guessed naira figure.");
        }

        var remainder = incomeMinor - risevestNgnMinor.Value - bambooNgnMinor.Value - 25_000_000 - 3_000_000 - 3_000_000;
        if (remainder < 0)
        {
            return new Result(lines, remainder,
                $"RED ALERT. The waterfall allocations exceed the ₦400,000 secondary income by {Money.FromMajor(Math.Abs(remainder) / 100m, Currency.Ngn)}.");
        }

        return new Result(lines, remainder,
            $"After the waterfall, {Money.FromMajor(remainder / 100m, Currency.Ngn)} stays in Kuda as a strategic buffer. It is not lifestyle money.");
    }
}

public static class BudgetStatusCalculator
{
    public static (BudgetStatus Status, string Sentence, decimal? PercentUsed) Evaluate(
        long budgetMinor,
        long actualMinor,
        int watchPercent,
        int warningPercent,
        int overPercent)
    {
        if (budgetMinor <= 0)
        {
            return (BudgetStatus.Unknown, "There is no budget amount for this category.", null);
        }

        var percent = (decimal)actualMinor * 100m / budgetMinor;
        var remaining = budgetMinor - actualMinor;
        BudgetStatus status;
        if (percent > overPercent) status = BudgetStatus.OverBudget;
        else if (percent >= warningPercent) status = BudgetStatus.Warning;
        else if (percent >= watchPercent) status = BudgetStatus.Watch;
        else status = BudgetStatus.Normal;

        var sentence = remaining >= 0
            ? $"Budget {Money.FromMajor(budgetMinor / 100m, Currency.Ngn)}. Actual {Money.FromMajor(actualMinor / 100m, Currency.Ngn)}. Remaining {Money.FromMajor(remaining / 100m, Currency.Ngn)}. Used {percent:0.#}%. Status: {StatusWords(status)}."
            : $"You've spent {Money.FromMajor(Math.Abs(remaining) / 100m, Currency.Ngn)} more than planned. Used {percent:0.#}%. Status: OVER BUDGET.";

        return (status, sentence, percent);
    }

    private static string StatusWords(BudgetStatus status) => status switch
    {
        BudgetStatus.Normal => "ON TRACK",
        BudgetStatus.Watch => "WATCH",
        BudgetStatus.Warning => "WARNING",
        BudgetStatus.OverBudget => "OVER BUDGET",
        _ => "UNKNOWN"
    };
}

public static class SinkingFundMath
{
    public static (long? RequiredMonthlyMinor, DateOnly? ProjectedCompletion, string Sentence) RequiredMonthly(
        long targetMinor,
        long? currentConfirmedMinor,
        DateOnly? deadline,
        DateOnly today,
        long plannedMonthlyMinor)
    {
        if (currentConfirmedMinor is null)
        {
            return (null, null, "Required monthly contribution is UNKNOWN until the current confirmed balance is entered.");
        }

        var remaining = targetMinor - currentConfirmedMinor.Value;
        if (remaining <= 0)
        {
            return (0, today, "This fund has already reached its target on confirmed balances.");
        }

        if (deadline is null)
        {
            if (plannedMonthlyMinor <= 0)
            {
                return (null, null, "No deadline and no planned monthly contribution, so a required monthly amount cannot be calculated.");
            }

            var months = (int)Math.Ceiling(remaining / (decimal)plannedMonthlyMinor);
            return (plannedMonthlyMinor, today.AddMonths(months),
                $"ESTIMATE: at {Money.FromMajor(plannedMonthlyMinor / 100m, Currency.Ngn)} per month, this fund completes in about {months} month(s).");
        }

        var monthsLeft = Math.Max(1, ((deadline.Value.Year - today.Year) * 12) + deadline.Value.Month - today.Month);
        var required = (long)Math.Ceiling(remaining / (decimal)monthsLeft);
        return (required, deadline,
            $"You need {Money.FromMajor(required / 100m, Currency.Ngn)} each month for the remaining {monthsLeft} month(s) to reach the target by {deadline:MMMM yyyy}.");
    }
}

public static class EmergencyTarget
{
    public static (long TargetMinor, string Sentence, bool IsProvisional) Resolve(
        long? postMoveEssentialMonthlyMinor,
        long provisionalTargetMinor)
    {
        if (postMoveEssentialMonthlyMinor is null)
        {
            return (provisionalTargetMinor,
                $"The emergency target is the provisional {Money.FromMajor(provisionalTargetMinor / 100m, Currency.Ngn)} until post-move essentials are entered.",
                true);
        }

        var target = postMoveEssentialMonthlyMinor.Value * 6;
        return (target,
            $"The emergency target is 6 × actual essential monthly expenses after moving = {Money.FromMajor(target / 100m, Currency.Ngn)}.",
            false);
    }
}

public static class RetirementCalculator
{
    public sealed record Scenario(string Name, decimal AnnualReturn, long? FutureValueMinor, string Sentence);

    public static IReadOnlyList<Scenario> Project(
        int? currentAge,
        int? retirementAge,
        long? currentInvestmentsMinor,
        bool investmentsConfirmed,
        long? monthlyContributionMinor,
        decimal? conservative,
        decimal? baseline,
        decimal? aggressive,
        decimal? inflation)
    {
        if (currentAge is null || retirementAge is null || monthlyContributionMinor is null
            || conservative is null || baseline is null || aggressive is null || inflation is null
            || !investmentsConfirmed || currentInvestmentsMinor is null)
        {
            return
            [
                new Scenario("Unavailable", 0, null,
                    "The retirement calculator stays closed until you enter current age, retirement age, confirmed current investments, monthly contribution, return assumptions, and inflation. ₦500m+ is an aspiration label, not a forecast.")
            ];
        }

        return new[]
        {
            Run("Conservative", conservative.Value, currentAge.Value, retirementAge.Value, currentInvestmentsMinor.Value, monthlyContributionMinor.Value, inflation.Value),
            Run("Base", baseline.Value, currentAge.Value, retirementAge.Value, currentInvestmentsMinor.Value, monthlyContributionMinor.Value, inflation.Value),
            Run("Aggressive", aggressive.Value, currentAge.Value, retirementAge.Value, currentInvestmentsMinor.Value, monthlyContributionMinor.Value, inflation.Value)
        };
    }

    private static Scenario Run(
        string name, decimal annualReturn, int age, int retireAge, long current, long monthly, decimal inflation)
    {
        var years = Math.Max(0, retireAge - age);
        var months = years * 12;
        var monthlyRate = (double)annualReturn / 12d;
        double fv = current / 100d;
        for (var i = 0; i < months; i++)
        {
            fv = (fv + monthly / 100d) * (1 + monthlyRate);
        }

        var real = inflation == 0 ? fv : fv / Math.Pow(1 + (double)inflation, years);
        var minor = (long)Math.Round(real * 100);
        return new Scenario(name, annualReturn, minor,
            $"ASSUMPTION, not a prediction. {name} uses {annualReturn:P1} nominal return and {inflation:P1} inflation over {years} years. Real purchasing-power estimate {Money.FromMajor(minor / 100m, Currency.Ngn)}.");
    }
}
