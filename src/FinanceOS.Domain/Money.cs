namespace FinanceOS.Domain;

public enum Currency
{
    Ngn = 1,
    Usd = 2,
    Gbp = 3,
    Eur = 4,
    Usdt = 5
}

/// <summary>
/// Money stored in minor units (kobo or cents). Never use floating point for amounts.
/// </summary>
public readonly record struct Money(long MinorUnits, Currency Currency)
{
    public const int Scale = 100;

    public decimal Major => MinorUnits / (decimal)Scale;

    public static Money Zero(Currency currency) => new(0, currency);

    public static Money NgnFromMajor(decimal naira) =>
        new(ToMinor(naira), Currency.Ngn);

    public static Money UsdFromMajor(decimal dollars) =>
        new(ToMinor(dollars), Currency.Usd);

    public static Money FromMajor(decimal major, Currency currency) =>
        new(ToMinor(major), currency);

    public static Money ParseMajor(string text, Currency currency)
    {
        if (!decimal.TryParse(text, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var major))
        {
            throw new DomainException($"Amount '{text}' is not a valid number.");
        }

        return FromMajor(major, currency);
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(MinorUnits + other.MinorUnits, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(MinorUnits - other.MinorUnits, Currency);
    }

    public Money Negate() => new(-MinorUnits, Currency);

    public bool IsZero => MinorUnits == 0;
    public bool IsPositive => MinorUnits > 0;
    public bool IsNegative => MinorUnits < 0;

    public static bool operator >(Money left, Money right)
    {
        left.EnsureSameCurrency(right);
        return left.MinorUnits > right.MinorUnits;
    }

    public static bool operator <(Money left, Money right)
    {
        left.EnsureSameCurrency(right);
        return left.MinorUnits < right.MinorUnits;
    }

    public static bool operator >=(Money left, Money right)
    {
        left.EnsureSameCurrency(right);
        return left.MinorUnits >= right.MinorUnits;
    }

    public static bool operator <=(Money left, Money right)
    {
        left.EnsureSameCurrency(right);
        return left.MinorUnits <= right.MinorUnits;
    }

    public string Format()
    {
        var symbol = Currency switch
        {
            Currency.Ngn => "₦",
            Currency.Usd => "$",
            Currency.Gbp => "£",
            Currency.Eur => "€",
            Currency.Usdt => "₮",
            _ => ""
        };
        return $"{symbol}{Major.ToString("#,##0.00", System.Globalization.CultureInfo.InvariantCulture)}";
    }

    public override string ToString() => Format();

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new DomainException($"Cannot combine {Currency} with {other.Currency}.");
        }
    }

    private static long ToMinor(decimal major) =>
        (long)decimal.Round(major * Scale, 0, MidpointRounding.AwayFromZero);
}

public enum Provenance
{
    Confirmed = 1,
    LastKnown = 2,
    Plan = 3,
    Expected = 4,
    Unknown = 5,
    Estimate = 6
}

public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}

public sealed class AmbiguousInputException : DomainException
{
    public IReadOnlyList<string> Questions { get; }

    public AmbiguousInputException(string message, IReadOnlyList<string> questions) : base(message)
    {
        Questions = questions;
    }
}
