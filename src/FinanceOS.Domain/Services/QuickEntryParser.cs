using System.Globalization;
using System.Text.RegularExpressions;

namespace FinanceOS.Domain.Services;

public sealed record ParsedTransactionDraft(
    TransactionType? Type,
    long? AmountMinor,
    Currency Currency,
    string? AccountHint,
    string? CounterpartyHint,
    string? CategoryHint,
    string? EnvelopeHint,
    DateOnly Date,
    string Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Questions,
    bool IsAmbiguous,
    bool IsBusiness,
    bool IsBetting);

public static class QuickEntryParser
{
    private static readonly Regex AmountRegex = new(
        @"(?<![.\d])(?<amount>\d{1,3}(?:,\d{3})+|\d+)(?:\.(?<frac>\d{1,2}))?(?<suffix>[kKmM])?",
        RegexOptions.Compiled);

    public static ParsedTransactionDraft Parse(
        string input,
        DateOnly today,
        IReadOnlyDictionary<string, string> accountHints)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Ambiguous("Say what happened, including the amount.", today);
        }

        var text = input.Trim();
        var lower = text.ToLowerInvariant();
        var questions = new List<string>();
        var tags = new List<string>();

        var amount = ExtractLargestAmount(text);
        if (amount is null)
        {
            questions.Add("What is the amount?");
        }

        var type = InferType(lower);
        var account = InferAccount(lower, accountHints);
        var counterparty = InferCounterparty(lower, accountHints, account);
        var category = InferCategory(lower);
        var envelope = category;
        var betting = lower.Contains("bet") || lower.Contains("sporty");
        var business = lower.Contains("railway") || lower.Contains("neon") || lower.Contains("matchpredictor")
                       || lower.Contains("domain") || lower.Contains("cursor");

        if (type is null) questions.Add("Is this income, an expense, a transfer, or something else?");
        if (account is null) questions.Add("Which account did this happen in?");
        if (type == TransactionType.Transfer && counterparty is null)
        {
            questions.Add("Which account is the other side of this transfer?");
        }

        if (betting)
        {
            category = "betting";
            envelope = "betting";
            tags.Add("betting");
        }

        if (type == TransactionType.Transfer)
        {
            category ??= "transfer";
        }

        var description = text;
        return new ParsedTransactionDraft(
            type,
            amount,
            Currency.Ngn,
            account,
            counterparty,
            category,
            envelope,
            today,
            description,
            tags,
            questions,
            questions.Count > 0,
            business,
            betting);
    }

    private static ParsedTransactionDraft Ambiguous(string question, DateOnly today) =>
        new(null, null, Currency.Ngn, null, null, null, null, today, "", [], [question], true, false, false);

    private static long? ExtractLargestAmount(string text)
    {
        long? best = null;
        foreach (Match match in AmountRegex.Matches(text))
        {
            var whole = match.Groups["amount"].Value.Replace(",", "", StringComparison.Ordinal);
            var frac = match.Groups["frac"].Success ? match.Groups["frac"].Value : "00";
            if (frac.Length == 1) frac += "0";
            if (long.TryParse(whole + frac, NumberStyles.None, CultureInfo.InvariantCulture, out var minor))
            {
                var suffix = match.Groups["suffix"].Value.ToLowerInvariant();
                minor = suffix switch
                {
                    "k" => minor * 1_000,
                    "m" => minor * 1_000_000,
                    _ => minor
                };
                if (best is null || minor > best)
                {
                    best = minor;
                }
            }
        }

        return best;
    }

    private static TransactionType? InferType(string lower)
    {
        if (lower.Contains("transferred") || lower.Contains("transfer") || lower.Contains("move ")
            || lower.Contains("moved"))
        {
            return TransactionType.Transfer;
        }

        if (lower.Contains("received") || lower.Contains("salary") || lower.Contains("income"))
        {
            return TransactionType.Income;
        }

        if (lower.Contains("refund")) return TransactionType.Refund;
        if (lower.Contains("fee") || lower.Contains("stamp duty")) return TransactionType.Fee;
        if (lower.Contains("withdrew") || lower.Contains("withdrawal")) return TransactionType.Withdrawal;
        if (lower.Contains("invest")) return TransactionType.Investment;
        if (lower.Contains("saved") || lower.Contains("savings")) return TransactionType.Savings;
        if (lower.Contains("spent") || lower.Contains("bought") || lower.Contains("paid")
            || lower.Contains("lunch") || lower.Contains("food") || lower.Contains("data"))
        {
            return TransactionType.Expense;
        }

        return null;
    }

    private static string? InferAccount(string lower, IReadOnlyDictionary<string, string> hints)
    {
        foreach (var (needle, slug) in hints)
        {
            if (lower.Contains("from " + needle)) return slug;
        }

        foreach (var (needle, slug) in hints)
        {
            if (lower.Contains(needle)) return slug;
        }

        if (lower.Contains("salary")) return "stanbic";
        if (lower.Contains("secondary")) return "kuda";
        return null;
    }

    private static string? InferCounterparty(string lower, IReadOnlyDictionary<string, string> hints, string? source)
    {
        foreach (var (needle, slug) in hints)
        {
            if (lower.Contains("to " + needle) || lower.Contains("into " + needle))
            {
                return slug == source ? null : slug;
            }
        }

        if (lower.Contains("rotating")) return "rotating-savings";
        if (lower.Contains("emergency")) return "cowrywise-emergency";
        if (lower.Contains("mmf")) return "cowrywise-mmf";
        if (lower.Contains("opay")) return source == "opay" ? null : "opay";
        return null;
    }

    private static string? InferCategory(string lower)
    {
        if (lower.Contains("lunch") || lower.Contains("food") || lower.Contains("breakfast") || lower.Contains("grocer"))
            return "food";
        if (lower.Contains("data")) return "data-airtime";
        if (lower.Contains("airtime")) return "data-airtime";
        if (lower.Contains("transport") || lower.Contains("uber") || lower.Contains("bolt")) return "transport";
        if (lower.Contains("family") || lower.Contains("aunt") || lower.Contains("brother")) return "family";
        if (lower.Contains("date") || lower.Contains("dating")) return "dating";
        if (lower.Contains("salary")) return "salary";
        if (lower.Contains("cursor") || lower.Contains("software")) return "career-business";
        if (lower.Contains("bet")) return "betting";
        return null;
    }
}
