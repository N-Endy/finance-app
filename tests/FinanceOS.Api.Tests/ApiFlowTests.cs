using System.Net;
using System.Net.Http.Json;
using FinanceOS.Application.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinanceOS.Api.Tests;

[CollectionDefinition("ApiFlow", DisableParallelization = true)]
public class ApiFlowCollection;

[Collection("ApiFlow")]
public class ApiFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiFlowTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    private async Task<HttpClient> SignedInClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var setup = await client.PostAsJsonAsync("/api/v1/auth/setup", new SetupRequest("Nnamdi", "nnamdi@local", "correct-horse-battery"));
        if (setup.StatusCode != HttpStatusCode.OK)
        {
            var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("nnamdi@local", "correct-horse-battery"));
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }

        return client;
    }

    [Fact]
    public async Task Setup_login_dashboard_and_explain_net_worth()
    {
        var client = await SignedInClient();

        var dashboard = await client.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard");
        Assert.NotNull(dashboard);
        Assert.Equal("UNKNOWN", dashboard!.NetWorth.Formatted);
        Assert.Contains("UNKNOWN", dashboard.SpendableSentence + dashboard.HousingSentence + string.Join(' ', dashboard.Unknowns.Select(u => u.Text)));

        var explain = await client.GetFromJsonAsync<ExplainDto>("/api/v1/explain/net-worth");
        Assert.NotNull(explain);
        Assert.Contains("UNKNOWN", explain!.Sentence);

        var holdings = await client.GetFromJsonAsync<HoldingDto[]>("/api/v1/holdings");
        Assert.NotNull(holdings);
        Assert.Contains(holdings!, h => h.Name.Contains("Rotating", StringComparison.OrdinalIgnoreCase));
        Assert.All(holdings!, h => Assert.Equal("UNKNOWN", h.Amount.Formatted));

        var pension = await client.GetFromJsonAsync<PensionDto>("/api/v1/pension");
        Assert.NotNull(pension);
        Assert.Equal("UNKNOWN", pension!.Balance.Formatted);

        var accounts = await client.GetFromJsonAsync<AccountDto[]>("/api/v1/accounts");
        Assert.NotNull(accounts);
        Assert.All(accounts!, a => Assert.True(a.LatestBalance is null || a.LatestBalance.Formatted == "UNKNOWN"));

        var txs = await client.GetFromJsonAsync<TransactionDto[]>("/api/v1/transactions");
        Assert.NotNull(txs);
        Assert.Empty(txs!);
    }

    [Fact]
    public async Task Assistant_falls_back_to_keyword_engine_without_openai()
    {
        var client = await SignedInClient();

        var response = await client.PostAsJsonAsync("/api/v1/assistant", new AssistantRequest("How much has MatchPredictor cost me?"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AssistantResponseDto>();
        Assert.NotNull(body);
        Assert.Contains("MatchPredictor", body!.Answer);
        Assert.DoesNotContain("I can answer from your ledger, plan, and confirmed balances", body.Answer);
    }

    [Fact]
    public async Task Assistant_unknown_and_betting_prompts_do_not_invent()
    {
        var client = await SignedInClient();

        var stanbic = await client.PostAsJsonAsync("/api/v1/assistant", new AssistantRequest("What is my Stanbic balance?"));
        Assert.Equal(HttpStatusCode.OK, stanbic.StatusCode);
        var stanbicBody = await stanbic.Content.ReadFromJsonAsync<AssistantResponseDto>();
        Assert.NotNull(stanbicBody);
        Assert.Contains("UNKNOWN", stanbicBody!.Answer);
        Assert.DoesNotContain("₦", stanbicBody.Answer);

        var betting = await client.PostAsJsonAsync("/api/v1/assistant", new AssistantRequest("Should I bet 5000 on Arsenal this weekend?"));
        Assert.Equal(HttpStatusCode.OK, betting.StatusCode);
        var bettingBody = await betting.Content.ReadFromJsonAsync<AssistantResponseDto>();
        Assert.NotNull(bettingBody);
        Assert.DoesNotContain("odds", bettingBody!.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("you should bet", bettingBody.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UNKNOWN", bettingBody.Answer + string.Join(' ', bettingBody.MissingFacts));
    }

    [Fact]
    public async Task Opening_snapshot_fills_reconciliation_without_inventing_actual()
    {
        var client = await SignedInClient();
        var accounts = await client.GetFromJsonAsync<AccountDto[]>("/api/v1/accounts");
        var stanbic = Assert.Single(accounts!, a => a.Slug == "stanbic");

        var incomplete = await ReadRecon(client, stanbic.Id);
        Assert.Null(incomplete.OpeningMinor);
        Assert.Null(incomplete.ExpectedClosingMinor);
        Assert.Null(incomplete.DifferenceMinor);
        Assert.Contains("opening", incomplete.Sentence, StringComparison.OrdinalIgnoreCase);

        var opening = await client.PostAsJsonAsync($"/api/v1/accounts/{stanbic.Id}/snapshots", new SnapshotRequest(
            100_000m, "NGN", DateOnly.FromDateTime(DateTime.UtcNow.Date), "Confirmed", "test opening", "opening"));
        Assert.Equal(HttpStatusCode.OK, opening.StatusCode);

        var afterOpening = await ReadRecon(client, stanbic.Id);
        Assert.Equal(10_000_000, afterOpening.OpeningMinor);
        Assert.NotNull(afterOpening.ExpectedClosingMinor);
        Assert.Null(afterOpening.ActualMinor);
        Assert.Null(afterOpening.DifferenceMinor);
    }

    private static async Task<ReconDto> ReadRecon(HttpClient client, Guid accountId)
    {
        var body = await client.GetFromJsonAsync<ReconDto>($"/api/v1/accounts/{accountId}/reconciliation");
        Assert.NotNull(body);
        return body!;
    }

    private sealed record ReconDto(
        long? OpeningMinor,
        long? ExpectedClosingMinor,
        long? ActualMinor,
        long? DifferenceMinor,
        string Sentence);

    [Fact]
    public async Task Failed_login_is_a_generic_bad_request()
    {
        var client = _factory.CreateClient();
        var failed = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("nobody@local", "wrong-password-here"));
        Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
    }

    [Fact]
    public async Task Transaction_realtime_balance_readjustment_lifecycle()
    {
        var client = await SignedInClient();
        var accounts = await client.GetFromJsonAsync<AccountDto[]>("/api/v1/accounts");
        var opay = Assert.Single(accounts!, a => a.Slug == "opay");

        // 1. Enter confirmed snapshot of 100,000 NGN
        var snapRes = await client.PostAsJsonAsync($"/api/v1/accounts/{opay.Id}/snapshots", new SnapshotRequest(
            100_000m, "NGN", DateOnly.FromDateTime(DateTime.UtcNow.Date), "Confirmed", "Opening balance test", "manual"));
        Assert.Equal(HttpStatusCode.OK, snapRes.StatusCode);

        accounts = await client.GetFromJsonAsync<AccountDto[]>("/api/v1/accounts");
        opay = Assert.Single(accounts!, a => a.Slug == "opay");
        Assert.NotNull(opay.LatestBalance);
        Assert.Equal(10_000_000, opay.LatestBalance!.Minor);

        // 2. Create expense transaction of 15,000 NGN
        var txRes = await client.PostAsJsonAsync("/api/v1/transactions", new TransactionWriteRequest(
            DateOnly.FromDateTime(DateTime.UtcNow.Date), "Expense", opay.Id, null, 15_000m, 0m, "NGN", null, null, null, null, null,
            "Groceries from store", "Supermarket", null, null, false, false));
        Assert.Equal(HttpStatusCode.OK, txRes.StatusCode);
        var createdTx = await txRes.Content.ReadFromJsonAsync<TransactionDto>();
        Assert.NotNull(createdTx);

        // Balance immediately readjusts to 85,000 NGN
        accounts = await client.GetFromJsonAsync<AccountDto[]>("/api/v1/accounts");
        opay = Assert.Single(accounts!, a => a.Slug == "opay");
        Assert.Equal(8_500_000, opay.LatestBalance!.Minor);

        // 3. Update expense transaction to 20,000 NGN
        var updateRes = await client.PutAsJsonAsync($"/api/v1/transactions/{createdTx!.Id}", new TransactionWriteRequest(
            DateOnly.FromDateTime(DateTime.UtcNow.Date), "Expense", opay.Id, null, 20_000m, 0m, "NGN", null, null, null, null, null,
            "Groceries updated", "Supermarket", null, null, false, false));
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        // Balance immediately readjusts to 80,000 NGN
        accounts = await client.GetFromJsonAsync<AccountDto[]>("/api/v1/accounts");
        opay = Assert.Single(accounts!, a => a.Slug == "opay");
        Assert.Equal(8_000_000, opay.LatestBalance!.Minor);

        // 4. Void transaction
        var voidRes = await client.PostAsync($"/api/v1/transactions/{createdTx.Id}/void", null);
        Assert.Equal(HttpStatusCode.OK, voidRes.StatusCode);

        // Balance immediately restores to 100,000 NGN
        accounts = await client.GetFromJsonAsync<AccountDto[]>("/api/v1/accounts");
        opay = Assert.Single(accounts!, a => a.Slug == "opay");
        Assert.Equal(10_000_000, opay.LatestBalance!.Minor);
    }
}
