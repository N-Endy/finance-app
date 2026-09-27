using FinanceOS.Application.Contracts;
using FinanceOS.Domain;
using FinanceOS.Domain.Entities;
using FinanceOS.Domain.Services;

namespace FinanceOS.Infrastructure.Services;

internal static class MoneyMapping
{
    public static MoneyDto Known(long minor, Currency currency, Provenance provenance, DateOnly? asOf = null) =>
        MoneyDto.Of(minor, currency, provenance, asOf);

    public static MoneyDto FromLine(ExplainLine line) =>
        line.AmountMinor is null
            ? MoneyDto.Unknown(line.Note ?? "Missing fact.")
            : line.Provenance == Provenance.Estimate
                ? MoneyDto.Estimate(line.AmountMinor.Value, line.Currency, line.Note ?? "Estimate.")
                : MoneyDto.Of(line.AmountMinor.Value, line.Currency, line.Provenance);

    public static ExplainLineDto Line(ExplainLine line) => new(line.Label, FromLine(line), line.Note);

    public static Currency ParseCurrency(string? value) =>
        string.Equals(value, "USD", StringComparison.OrdinalIgnoreCase) ? Currency.Usd : Currency.Ngn;

    public static TransactionType ParseType(string value) =>
        Enum.Parse<TransactionType>(value, true);

    public static string HealthName(AccountHealth health) => health switch
    {
        AccountHealth.Healthy => "Healthy",
        AccountHealth.Watch => "Watch",
        AccountHealth.Attention => "Attention",
        _ => "Unknown"
    };

    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    public static (DateOnly Start, DateOnly End) MonthBounds(DateOnly day) =>
        (new DateOnly(day.Year, day.Month, 1), new DateOnly(day.Year, day.Month, 1).AddMonths(1).AddDays(-1));
}
