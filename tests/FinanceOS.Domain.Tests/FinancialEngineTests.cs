using FinanceOS.Domain;
using FinanceOS.Domain.Entities;
using FinanceOS.Domain.Services;

namespace FinanceOS.Domain.Tests;

public class FinancialEngineTests
{
    [Fact]
    public void Salary_allocations_sum_to_543000()
    {
        long[] lines =
        [
            4_500_000, 8_500_000, 3_500_000, 1_200_000, 2_000_000, 3_000_000, 4_500_000, 2_000_000,
            5_000_000, 5_000_000, 2_000_000, 10_000_000, 500_000, 2_600_000
        ];
        Assert.Equal(54_300_000, AllocationMath.SumFixedLines(lines));
        Assert.Equal(0, AllocationMath.Unallocated(54_300_000, 54_300_000));
    }

    [Fact]
    public void Over_allocation_is_a_hard_error()
    {
        var error = Assert.Throws<DomainException>(() =>
            AllocationMath.EnsureSalaryBalances(54_300_000, 54_400_000));
        Assert.Contains("RED ALERT", error.Message);
    }

    [Fact]
    public void Stanbic_180000_fully_assigned_is_spendable_zero()
    {
        var assignments = new List<(string, AccountRole, bool, string, EnvelopeClass, long, Currency)>
        {
            ("Stanbic", AccountRole.Clearing, false, "Food", EnvelopeClass.Spend, 3_000_000, Currency.Ngn),
            ("Stanbic", AccountRole.Clearing, false, "Bills", EnvelopeClass.Committed, 2_000_000, Currency.Ngn),
            ("Stanbic", AccountRole.Clearing, false, "Upcoming savings", EnvelopeClass.Savings, 5_000_000, Currency.Ngn),
            ("Stanbic", AccountRole.Clearing, false, "Family", EnvelopeClass.Committed, 3_000_000, Currency.Ngn),
            ("Stanbic", AccountRole.Clearing, false, "Operating reserve", EnvelopeClass.Buffer, 5_000_000, Currency.Ngn)
        };

        var result = SpendableCalculator.FromAssignments(assignments, Currency.Ngn);
        Assert.Equal(0, result.SpendableMinor);
        Assert.Contains("₦0", result.Sentence);
    }

    [Fact]
    public void Own_account_transfer_does_not_change_net_worth()
    {
        var items = new List<(string, long, Currency, Provenance, bool, bool, bool)>
        {
            ("Stanbic", 10_000_000, Currency.Ngn, Provenance.Confirmed, true, false, false),
            ("Cowrywise Emergency", 5_000_000, Currency.Ngn, Provenance.Confirmed, true, false, false)
        };

        var before = NetWorthCalculator.Calculate(items, null);
        items[0] = ("Stanbic", 5_000_000, Currency.Ngn, Provenance.Confirmed, true, false, false);
        items[1] = ("Cowrywise Emergency", 10_000_000, Currency.Ngn, Provenance.Confirmed, true, false, false);
        var after = NetWorthCalculator.Calculate(items, null);

        Assert.Equal(before.ResultMinor, after.ResultMinor);
        Assert.Equal(15_000_000, after.ResultMinor);
    }

    [Fact]
    public void Ledger_mismatch_is_unreconciled()
    {
        var result = ReconciliationCalculator.Calculate(
            Guid.NewGuid(), "Stanbic", 10_000_000, 0, 0, 0, 0, 9_250_000, new DateOnly(2026, 9, 27),
            Provenance.Confirmed);

        Assert.Equal(ReconciliationStatus.Unreconciled, result.Status);
        Assert.Equal(-750_000, result.DifferenceMinor);
        Assert.Contains("UNRECONCILED", result.Sentence);
        Assert.Contains("7,500.00", result.Sentence);
    }

    [Fact]
    public void Missing_opening_balance_is_incomplete_not_invented()
    {
        var result = ReconciliationCalculator.Calculate(
            Guid.NewGuid(), "Stanbic", null, 54_300_000, 20_000_000, 10_375, 0, null, null, null);

        Assert.Equal(ReconciliationStatus.Incomplete, result.Status);
        Assert.Null(result.ExpectedClosingMinor);
        Assert.Contains("incomplete", result.Sentence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Betting_expense_violates_zero_allocation_rule()
    {
        var tx = new LedgerTransaction
        {
            Type = TransactionType.Expense,
            AmountMinor = 1_100_000,
            Description = "SportyBet funding"
        };

        var checks = RuleEngine.EvaluateTransaction(tx, "betting", "opay", "betting");
        Assert.Contains(checks, c => c.Violated && c.Code == "betting");
    }

    [Fact]
    public void Secondary_income_remainder_unknown_without_actual_dollar_charges()
    {
        var result = SecondaryWaterfall.Apply(40_000_000, null, null);
        Assert.Null(result.RemainderMinor);
        Assert.Contains("UNKNOWN", result.Sentence);
    }

    [Fact]
    public void Secondary_income_remainder_known_when_charges_recorded()
    {
        var result = SecondaryWaterfall.Apply(40_000_000, 4_000_000, 4_800_000);
        Assert.Equal(200_000, result.RemainderMinor);
        Assert.Contains("stays in Kuda", result.Sentence);
    }

    [Fact]
    public void Last_known_and_expected_items_are_excluded_from_confirmed_net_worth()
    {
        var items = new List<(string, long, Currency, Provenance, bool, bool, bool)>
        {
            ("Emergency", 153_748_800, Currency.Ngn, Provenance.LastKnown, true, false, false),
            ("Rotating payout", 120_000_000, Currency.Ngn, Provenance.Expected, true, true, false),
            ("Bamboo US", 60_000, Currency.Usd, Provenance.LastKnown, true, false, false)
        };

        var result = NetWorthCalculator.Calculate(items, null);
        Assert.Null(result.ResultMinor);
        Assert.Equal(Provenance.Unknown, result.ResultProvenance);
        Assert.Contains("UNKNOWN", result.Sentence);
        Assert.Contains("USD", result.Sentence);
    }

    [Fact]
    public void Housing_projection_does_not_double_count_rotating_contributions()
    {
        var result = HousingProjection.Project(
            0, 3_733_600, 5, 25_000_000, 500_000, 120_000_000, 300_000_000, new DateOnly(2026, 9, 27));

        Assert.Contains("not added on top", result.Lines.Single(l => l.Label.Contains("rotating")).Note);
        Assert.True(result.ProjectedDateIsEstimate);
    }

    [Fact]
    public void Housing_move_date_unknown_when_piggyvest_unknown()
    {
        var result = HousingProjection.Project(
            null, 3_733_600, 5, 25_000_000, 500_000, 120_000_000, 300_000_000, new DateOnly(2026, 9, 27));
        Assert.Null(result.ProjectedMoveDate);
        Assert.Contains("UNKNOWN", result.Sentence);
    }

    [Fact]
    public void Quick_entry_parses_lunch_and_requires_account_if_missing()
    {
        var draft = QuickEntryParser.Parse("Lunch 4300", new DateOnly(2026, 8, 27), new Dictionary<string, string>
        {
            ["opay"] = "opay",
            ["stanbic"] = "stanbic"
        });

        Assert.Equal(430_000, draft.AmountMinor);
        Assert.Equal(TransactionType.Expense, draft.Type);
        Assert.Equal("food", draft.CategoryHint);
        Assert.True(draft.IsAmbiguous);
        Assert.Contains(draft.Questions, q => q.Contains("account", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Quick_entry_parses_transfer_to_rotating_savings()
    {
        var draft = QuickEntryParser.Parse(
            "Transferred 100k to rotating savings from OPay",
            new DateOnly(2026, 8, 27),
            new Dictionary<string, string> { ["opay"] = "opay" });

        Assert.Equal(10_000_000, draft.AmountMinor);
        Assert.Equal(TransactionType.Transfer, draft.Type);
        Assert.Equal("opay", draft.AccountHint);
        Assert.Equal("rotating-savings", draft.CounterpartyHint);
        Assert.False(draft.IsAmbiguous);
    }

    [Fact]
    public void Quick_entry_prefers_from_account_on_transfers()
    {
        var draft = QuickEntryParser.Parse(
            "Transferred 50000 from Stanbic to OPay",
            new DateOnly(2026, 9, 27),
            new Dictionary<string, string> { ["opay"] = "opay", ["stanbic"] = "stanbic" });

        Assert.Equal(5_000_000, draft.AmountMinor);
        Assert.Equal("stanbic", draft.AccountHint);
        Assert.Equal("opay", draft.CounterpartyHint);
        Assert.False(draft.IsAmbiguous);
    }

    [Fact]
    public void Emergency_target_stays_provisional_without_post_move_essentials()
    {
        var result = EmergencyTarget.Resolve(null, 240_000_000);
        Assert.True(result.IsProvisional);
        Assert.Equal(240_000_000, result.TargetMinor);
        Assert.Contains("provisional", result.Sentence);
    }

    [Fact]
    public void Retirement_calculator_stays_closed_without_inputs()
    {
        var scenarios = RetirementCalculator.Project(null, null, null, false, null, null, null, null, null);
        Assert.Single(scenarios);
        Assert.Null(scenarios[0].FutureValueMinor);
        Assert.Contains("stays closed", scenarios[0].Sentence);
    }
}
