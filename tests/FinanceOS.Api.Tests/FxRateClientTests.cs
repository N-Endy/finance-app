using System.Net;
using System.Text;
using FinanceOS.Infrastructure.Services;

namespace FinanceOS.Api.Tests;

public class FxRateClientTests
{
    [Fact]
    public async Task Fetch_reads_ngn_rate_from_feed()
    {
        var client = new FxRateClient(new HttpClient(new StubHandler
        {
            Reply = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"rates":{"NGN":1480.25},"time_last_update_utc":"Sun, 27 Sep 2026 00:00:00 +0000"}""", Encoding.UTF8, "application/json")
            }
        })
        { BaseAddress = new Uri("https://open.er-api.com/") });

        var fetched = await client.FetchUsdNgnAsync(CancellationToken.None);

        Assert.NotNull(fetched);
        Assert.Equal(1480.25m, fetched.Value.Rate);
        Assert.Equal(new DateOnly(2026, 9, 27), fetched.Value.AsOf);
    }

    [Fact]
    public async Task Fetch_returns_null_on_error_or_missing_ngn()
    {
        var failing = new FxRateClient(new HttpClient(new StubHandler
        {
            Reply = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        })
        { BaseAddress = new Uri("https://open.er-api.com/") });
        Assert.Null(await failing.FetchUsdNgnAsync(CancellationToken.None));

        var missing = new FxRateClient(new HttpClient(new StubHandler
        {
            Reply = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"rates":{"EUR":0.9}}""", Encoding.UTF8, "application/json")
            }
        })
        { BaseAddress = new Uri("https://open.er-api.com/") });
        Assert.Null(await missing.FetchUsdNgnAsync(CancellationToken.None));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpResponseMessage Reply { get; set; } = new(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Reply);
    }
}
