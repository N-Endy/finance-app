using FinanceOS.Application.Abstractions;

namespace FinanceOS.Infrastructure.Feeds;

public sealed class ManualAccountFeed : IAccountFeed
{
    public string Name => "manual";

    public Task<IReadOnlyList<FeedTransaction>> ReadAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FeedTransaction>>([]);
}
