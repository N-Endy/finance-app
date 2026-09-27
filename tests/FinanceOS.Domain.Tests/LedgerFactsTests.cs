using FinanceOS.Domain.Services;

namespace FinanceOS.Domain.Tests;

public class LedgerFactsTests
{
    [Fact]
    public void Packet_json_only_includes_provided_figures()
    {
        var facts = new List<LedgerFact>
        {
            new("MatchPredictor net", "₦1,200.00", "confirmed", 120_000),
            new("Stanbic current balance", "UNKNOWN", "unknown")
        };

        var json = LedgerFacts.ToJson(facts);

        Assert.Contains("MatchPredictor net", json);
        Assert.Contains("UNKNOWN", json);
        Assert.DoesNotContain("invented", json);
        Assert.False(LedgerFacts.HasInventedBalance(facts, "Stanbic current balance"));
    }

    [Fact]
    public void Missing_labels_are_the_unknown_facts()
    {
        var facts = new List<LedgerFact>
        {
            new("Family support this year", "₦0.00", "confirmed", 0),
            new("Stanbic current balance", "UNKNOWN", "unknown")
        };

        var missing = LedgerFacts.MissingLabels(facts);

        Assert.Contains("Stanbic current balance", missing);
        Assert.DoesNotContain("Family support this year", missing);
    }

    [Fact]
    public void System_prompt_forbids_invented_balances_and_betting_advice()
    {
        Assert.Contains("UNKNOWN", LedgerFacts.SystemPrompt);
        Assert.Contains("Do not invent", LedgerFacts.SystemPrompt);
        Assert.Contains("Do not give betting advice", LedgerFacts.SystemPrompt);
    }
}
