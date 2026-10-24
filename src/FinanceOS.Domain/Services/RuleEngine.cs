using FinanceOS.Domain.Entities;

namespace FinanceOS.Domain.Services;

public sealed record RuleCheck(string Code, string Message, bool Violated);

public static class RuleEngine
{
    public static IReadOnlyList<RuleCheck> EvaluateTransaction(
        LedgerTransaction tx,
        string? categorySlug,
        string? accountSlug,
        string? envelopeSlug)
    {
        var checks = new List<RuleCheck>();

        if (categorySlug == "betting" || envelopeSlug == "betting")
        {
            checks.Add(new RuleCheck(
                "betting",
                "Betting expense was recorded against a zero-allocation category.",
                true));
        }

        if (accountSlug?.Contains("emergency", StringComparison.OrdinalIgnoreCase) == true && tx.Type is TransactionType.Expense or TransactionType.Withdrawal)
        {
            var purpose = (tx.Description + " " + tx.Notes).ToLowerInvariant();
            var nonEmergency = purpose.Contains("bet") || purpose.Contains("date") || purpose.Contains("shop")
                            || purpose.Contains("holiday") || purpose.Contains("lifestyle");
            if (nonEmergency)
            {
                checks.Add(new RuleCheck(
                    "emergency",
                    "Emergency reserve funds should be reserved for genuine emergencies only.",
                    true));
            }
        }

        if (tx.IsTransfer && tx.Type == TransactionType.Expense)
        {
            checks.Add(new RuleCheck(
                "ledger",
                "A transfer was classified as an expense. Transfers between your own accounts are capital movements, not expenses.",
                true));
        }

        return checks;
    }

    public static RuleCheck LifestyleOnSecondary(bool lifestyleDependsOnSecondary) =>
        new("income",
            "Lifestyle expenses should be funded from predictable primary income. Secondary revenue is strategic.",
            lifestyleDependsOnSecondary);
}
