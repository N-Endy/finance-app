using System.Net;
using System.Text;
using FinanceOS.Infrastructure.Services;

namespace FinanceOS.Api.Tests;

public class OpenAiLedgerClientTests
{
    [Fact]
    public async Task Complete_sends_gpt6_luna_without_temperature()
    {
        var handler = new StubHandler
        {
            Reply = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"choices":[{"message":{"content":"MatchPredictor net is recorded."}}]}""", Encoding.UTF8, "application/json")
            }
        };
        var client = OpenAiLedgerClient.Create(new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/") }, "test-key", null);

        var text = await client.CompleteAsync("system", "user", CancellationToken.None);
        var body = handler.LastBody ?? "";

        Assert.Equal("MatchPredictor net is recorded.", text);
        Assert.Contains("\"model\":\"gpt-6-luna\"", body.Replace(" ", ""));
        Assert.Contains("reasoning_effort", body);
        Assert.DoesNotContain("temperature", body);
        Assert.DoesNotContain("top_p", body);
    }

    [Fact]
    public async Task Complete_returns_null_on_error_or_empty()
    {
        var failing = OpenAiLedgerClient.Create(new HttpClient(new StubHandler
        {
            Reply = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        })
        { BaseAddress = new Uri("https://api.openai.com/") }, "test-key", "gpt-6-luna");
        Assert.Null(await failing.CompleteAsync("s", "u", CancellationToken.None));

        var empty = OpenAiLedgerClient.Create(new HttpClient(new StubHandler
        {
            Reply = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") }
        })
        { BaseAddress = new Uri("https://api.openai.com/") }, "test-key", "gpt-6-luna");
        Assert.Null(await empty.CompleteAsync("s", "u", CancellationToken.None));

        var unset = OpenAiLedgerClient.Create(new HttpClient(), null, null);
        Assert.False(unset.IsConfigured);
        Assert.Null(await unset.CompleteAsync("s", "u", CancellationToken.None));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpResponseMessage Reply { get; set; } = new(HttpStatusCode.OK);
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return Reply;
        }
    }
}
