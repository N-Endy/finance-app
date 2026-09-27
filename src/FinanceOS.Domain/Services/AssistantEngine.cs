namespace FinanceOS.Domain.Services;

public sealed record AssistantAnswer(
    string Question,
    string Answer,
    IReadOnlyList<ExplainLine> Evidence,
    bool UsedEstimate,
    IReadOnlyList<string> MissingFacts);

public static class AssistantEngine
{
    public static AssistantAnswer AffordPurchase(
        long amountMinor,
        long spendableMinor,
        long remainingSpendBudgetMinor,
        bool spendableKnown)
    {
        if (!spendableKnown)
        {
            return Missing("Can I afford this purchase?",
                "Affordability is UNKNOWN until daily-spending envelopes and current balances are entered.",
                ["Actually spendable cash", "Remaining spend-envelope assignments"]);
        }

        if (amountMinor <= spendableMinor)
        {
            return new AssistantAnswer(
                "Can I afford this purchase?",
                $"This {Money.FromMajor(amountMinor / 100m, Currency.Ngn)} purchase fits inside actually spendable money of {Money.FromMajor(spendableMinor / 100m, Currency.Ngn)}. Remaining planned spending after that would be {Money.FromMajor((remainingSpendBudgetMinor - amountMinor) / 100m, Currency.Ngn)}.",
                [Line("Actually spendable", spendableMinor), Line("Purchase", amountMinor)],
                false,
                []);
        }

        if (amountMinor <= remainingSpendBudgetMinor)
        {
            return new AssistantAnswer(
                "Can I afford this purchase?",
                $"Actually spendable cash is only {Money.FromMajor(spendableMinor / 100m, Currency.Ngn)}. The purchase could fit the remaining planned spending envelopes ({Money.FromMajor(remainingSpendBudgetMinor / 100m, Currency.Ngn)}), but that money already has a job. Confirm which envelope you want to reduce.",
                [Line("Actually spendable", spendableMinor), Line("Remaining planned spending", remainingSpendBudgetMinor)],
                false,
                []);
        }

        return new AssistantAnswer(
            "Can I afford this purchase?",
            $"This {Money.FromMajor(amountMinor / 100m, Currency.Ngn)} purchase is larger than actually spendable money and larger than remaining planned spending. Paying for it would take money from another job.",
            [Line("Actually spendable", spendableMinor), Line("Remaining planned spending", remainingSpendBudgetMinor)],
            false,
            []);
    }

    public static AssistantAnswer WeekendSpend(long remainingSpendMinor, int remainingDaysInMonth)
    {
        if (remainingDaysInMonth <= 0)
        {
            return new AssistantAnswer(
                "How much can I safely spend this weekend?",
                $"You have {Money.FromMajor(remainingSpendMinor / 100m, Currency.Ngn)} left in spend envelopes. The month is at its end, so treat that as the ceiling.",
                [Line("Remaining spend envelopes", remainingSpendMinor)],
                false,
                []);
        }

        var weekendShare = remainingSpendMinor * 2 / remainingDaysInMonth;
        return new AssistantAnswer(
            "How much can I safely spend this weekend?",
            $"ESTIMATE based on spreading remaining spend envelopes evenly: about {Money.FromMajor(weekendShare / 100m, Currency.Ngn)} over two days. Remaining planned spending this month is {Money.FromMajor(remainingSpendMinor / 100m, Currency.Ngn)}.",
            [Line("Remaining spend envelopes", remainingSpendMinor)],
            true,
            []);
    }

    public static AssistantAnswer FromFacts(string question, string answer, IReadOnlyList<ExplainLine> evidence, params string[] missing)
    {
        return new AssistantAnswer(question, answer, evidence, false, missing);
    }

    private static AssistantAnswer Missing(string question, string answer, IReadOnlyList<string> missing) =>
        new(question, answer, [], false, missing);

    private static ExplainLine Line(string label, long amount) =>
        new(label, amount, Currency.Ngn, Provenance.Confirmed, null);
}
