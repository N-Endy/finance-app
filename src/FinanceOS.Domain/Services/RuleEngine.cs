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
                "Betting activity was recorded. The current plan allocates ₦0 to personal betting. This is awareness, not a staking recommendation.",
                true));
        }

        if (accountSlug == "cowrywise-emergency" && tx.Type is TransactionType.Expense or TransactionType.Withdrawal)
        {
            var purpose = (tx.Description + " " + tx.Notes).ToLowerInvariant();
            var forbidden = purpose.Contains("bet") || purpose.Contains("date") || purpose.Contains("shop")
                            || purpose.Contains("family") || purpose.Contains("move") || purpose.Contains("housing")
                            || purpose.Contains("invest") || purpose.Contains("lifestyle");
            if (forbidden)
            {
                checks.Add(new RuleCheck(
                    "emergency",
                    "Emergency fund is being used for a purpose the plan forbids. Emergency money is for genuine emergencies only.",
                    true));
            }
        }

        if (tx.IsTransfer && tx.Type == TransactionType.Expense)
        {
            checks.Add(new RuleCheck(
                "ledger",
                "A transfer was classified as an expense. Transfers between your own accounts are not spending.",
                true));
        }

        if (accountSlug == "opay" && tx.Type == TransactionType.Transfer && tx.AmountMinor > 0
            && (tx.Description.Contains("top", StringComparison.OrdinalIgnoreCase)
                || tx.Description.Contains("fund", StringComparison.OrdinalIgnoreCase)))
        {
            checks.Add(new RuleCheck(
                "opay-allowance",
                "Money is being added to OPay. Compare this with the planned monthly spending allowance before confirming.",
                false));
        }

        return checks;
    }

    public static RuleCheck LifestyleOnSecondary(bool lifestyleDependsOnSecondary) =>
        new("income",
            "Lifestyle must run on the ₦543,000 salary. The ₦400,000 secondary income is strategic.",
            lifestyleDependsOnSecondary);
}
