using System.Text.Json;

namespace FinanceOS.Domain.Services;

public sealed record LedgerFact(string Label, string Value, string Provenance, long? Minor = null);

public static class LedgerFacts
{
    public const string SystemPrompt =
        """
        You are Finance OS. Answer only from the facts JSON in the user message.
        If a figure is missing or labelled unknown, say UNKNOWN. Do not invent balances, FX, pension, revenue, or winnings.
        Do not give betting advice. Do not tell the user to spend the emergency fund or housing capital.
        Do not record or move money. Use plain language. One short paragraph unless a list is required.
        """;

    public static string ToJson(IReadOnlyList<LedgerFact> facts) =>
        JsonSerializer.Serialize(facts.Select(f => new
        {
            label = f.Label,
            value = f.Value,
            provenance = f.Provenance
        }));

    public static IReadOnlyList<string> MissingLabels(IReadOnlyList<LedgerFact> facts) =>
        facts
            .Where(f => f.Value.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase)
                        || f.Provenance.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Label)
            .ToList();

    public static bool HasInventedBalance(IReadOnlyList<LedgerFact> facts, string label) =>
        facts.Any(f => f.Label.Equals(label, StringComparison.OrdinalIgnoreCase)
                       && !f.Value.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase)
                       && f.Minor is null
                       && f.Provenance.Equals("unknown", StringComparison.OrdinalIgnoreCase));
}
