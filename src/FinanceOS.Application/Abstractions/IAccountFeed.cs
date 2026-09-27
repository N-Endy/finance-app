namespace FinanceOS.Application.Abstractions;

/// <summary>
/// Future bank or open-banking feeds implement this port.
/// The ledger never depends on a specific Nigerian bank API.
/// </summary>
public interface IAccountFeed
{
    string Name { get; }
    Task<IReadOnlyList<FeedTransaction>> ReadAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

public sealed record FeedTransaction(
    DateOnly Date,
    string ExternalId,
    string AccountSlug,
    decimal Amount,
    string Currency,
    string Description);
