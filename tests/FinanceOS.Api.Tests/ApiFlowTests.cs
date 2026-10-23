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

    [Fact]
    public async Task MultiTenant_Isolation_And_PaydayAssignment_Flow()
    {
        // 1. Register a new universal tenant: Alice in London with USD base currency & 50/30/20 archetype
        var aliceClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var regRes = await aliceClient.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(
            "Alice", "alice@example.com", "complex-password-12345", "USD", "fifty_thirty_twenty"));
        Assert.Equal(HttpStatusCode.OK, regRes.StatusCode);

        var aliceStatus = await regRes.Content.ReadFromJsonAsync<AuthStatusDto>();
        Assert.NotNull(aliceStatus);
        Assert.True(aliceStatus!.Authenticated);
        Assert.Equal("Alice", aliceStatus.DisplayName);
        Assert.Equal("USD", aliceStatus.BaseCurrency);
        Assert.True(aliceStatus.OnboardingCompleted);

        // 2. Alice's accounts: should have Primary Checking & Savings & Wealth Vault in USD, NOT Nigerian banks
        var aliceAccounts = await aliceClient.GetFromJsonAsync<AccountDto[]>("/api/v1/accounts");
        Assert.NotNull(aliceAccounts);
        Assert.NotEmpty(aliceAccounts!);
        Assert.DoesNotContain(aliceAccounts!, a => a.Slug == "opay" || a.Slug == "stanbic");
        Assert.Contains(aliceAccounts!, a => a.Slug == "primary-checking");

        // 3. Alice's envelopes: should have 50% Needs, 30% Wants, 20% Savings
        var aliceEnvelopes = await aliceClient.GetFromJsonAsync<EnvelopeDto[]>("/api/v1/envelopes");
        Assert.NotNull(aliceEnvelopes);
        Assert.Contains(aliceEnvelopes!, e => e.Slug == "needs-50");
        Assert.Contains(aliceEnvelopes!, e => e.Slug == "wants-30");
        Assert.Contains(aliceEnvelopes!, e => e.Slug == "savings-20");

        // 4. Payday "Give Every Dollar A Job" Flow:
        // Alice deposits $3,000 into Primary Checking
        var checking = aliceAccounts!.First(a => a.Slug == "primary-checking");
        var snapRes = await aliceClient.PostAsJsonAsync($"/api/v1/accounts/{checking.Id}/snapshots", new SnapshotRequest(
            3_000m, "USD", DateOnly.FromDateTime(DateTime.UtcNow.Date), "Confirmed", "Payday direct deposit"));
        Assert.Equal(HttpStatusCode.OK, snapRes.StatusCode);

        // Unassigned cash should be $3,000
        var unassignedRes = await aliceClient.GetFromJsonAsync<UnassignedCashDto>("/api/v1/budget/unassigned");
        Assert.NotNull(unassignedRes);
        Assert.Equal("$3,000.00", unassignedRes!.UnassignedCash.Formatted);
        Assert.False(unassignedRes.IsFullyAssigned);

        // Alice assigns every dollar a job:
        // Needs (50%) -> $1,500, Wants (30%) -> $900, Savings (20%) -> $600
        var envNeeds = aliceEnvelopes!.First(e => e.Slug == "needs-50");
        var envWants = aliceEnvelopes!.First(e => e.Slug == "wants-30");
        var envSavings = aliceEnvelopes!.First(e => e.Slug == "savings-20");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var assignBatchRes = await aliceClient.PostAsJsonAsync("/api/v1/budget/assign-batch", new BatchEnvelopeAssignRequest(
        [
            new BatchEnvelopeAssignItem(envNeeds.Id, 1_500m, checking.Id, today, "Rent & Food allocation"),
            new BatchEnvelopeAssignItem(envWants.Id, 900m, checking.Id, today, "Leisure & Dining allocation"),
            new BatchEnvelopeAssignItem(envSavings.Id, 600m, checking.Id, today, "Emergency & ETF allocation")
        ]));
        Assert.Equal(HttpStatusCode.OK, assignBatchRes.StatusCode);

        // Check unassigned cash again: should be $0.00, fully assigned!
        unassignedRes = await aliceClient.GetFromJsonAsync<UnassignedCashDto>("/api/v1/budget/unassigned");
        Assert.NotNull(unassignedRes);
        Assert.Equal("$0.00", unassignedRes!.UnassignedCash.Formatted);
        Assert.True(unassignedRes.IsFullyAssigned);

        // 5. Total Tenant Data Isolation:
        // Alice cannot access Tenant 1 (Nnamdi)'s accounts
        var nnamdiClient = await SignedInClient();
        var nnamdiAccounts = await nnamdiClient.GetFromJsonAsync<AccountDto[]>("/api/v1/accounts");
        var nnamdiOpay = Assert.Single(nnamdiAccounts!, a => a.Slug == "opay");

        // Alice attempts to get reconciliation on Nnamdi's Opay account -> should fail with 400 DomainException
        var maliciousAccess = await aliceClient.GetAsync($"/api/v1/accounts/{nnamdiOpay.Id}/reconciliation");
        Assert.Equal(HttpStatusCode.BadRequest, maliciousAccess.StatusCode);

        // 6. Data Export & Account Deletion:
        var exportRes = await aliceClient.GetAsync("/api/v1/data/export");
        Assert.Equal(HttpStatusCode.OK, exportRes.StatusCode);
        var exportJson = await exportRes.Content.ReadAsStringAsync();
        Assert.Contains("alice@example.com", exportJson);

        // Alice deletes her account
        var deleteRes = await aliceClient.DeleteAsync("/api/v1/data/account");
        Assert.Equal(HttpStatusCode.OK, deleteRes.StatusCode);

        // Nnamdi's data is completely intact!
        var nnamdiDashboard = await nnamdiClient.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard");
        Assert.NotNull(nnamdiDashboard);
    }

    [Fact]
    public async Task AiCoPilot_endpoints_operate_seamlessly()
    {
        var client = await SignedInClient();

        // 1. Natural Language Parse
        var parseRes = await client.PostAsJsonAsync("/api/v1/transactions/ai-parse", new NaturalLanguageParseRequest("Spent $25.50 at Blue Bottle for coffee yesterday"));
        Assert.Equal(HttpStatusCode.OK, parseRes.StatusCode);
        var parsed = await parseRes.Content.ReadFromJsonAsync<NaturalLanguageParseResultDto>();
        Assert.NotNull(parsed);
        // Fallback or LLM should yield a plausible amount and non-empty summary
        Assert.True(parsed.Success);
        Assert.NotNull(parsed.ProposedTransaction);
        Assert.Equal(25.50m, parsed.ProposedTransaction!.Amount);

        // 2. Velocity Radar
        var radarRes = await client.GetAsync("/api/v1/budget/velocity-radar");
        Assert.Equal(HttpStatusCode.OK, radarRes.StatusCode);
        var radar = await radarRes.Content.ReadFromJsonAsync<BurnVelocityRadarDto>();
        Assert.NotNull(radar);
        Assert.NotNull(radar.Items);

        // 3. AI Payday Recommendation
        var recRes = await client.GetAsync("/api/v1/budget/ai-recommendation");
        Assert.Equal(HttpStatusCode.OK, recRes.StatusCode);
        var rec = await recRes.Content.ReadFromJsonAsync<AiPaydayAllocationPlanDto>();
        Assert.NotNull(rec);
        Assert.NotNull(rec.Recommendations);
    }

    [Fact]
    public async Task HouseholdCollaboration_allows_joint_transparency_and_multi_member_attribution()
    {
        // 1. Register Alice
        var aliceClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var aliceReg = await aliceClient.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("Alice Household", "alice_house@example.com", "Password1234!", "USD", "everyday-finances"));
        Assert.Equal(HttpStatusCode.OK, aliceReg.StatusCode);

        // 2. Alice gets her household workspace
        var aliceWs = await aliceClient.GetFromJsonAsync<HouseholdWorkspaceDto>("/api/v1/household");
        Assert.NotNull(aliceWs);
        Assert.Equal("Owner", aliceWs!.MyRole);
        Assert.Single(aliceWs.Members);

        // 3. Alice creates an invite link
        var inviteRes = await aliceClient.PostAsJsonAsync("/api/v1/household/invite", new HouseholdInviteRequest("Partner"));
        Assert.Equal(HttpStatusCode.OK, inviteRes.StatusCode);
        var invite = await inviteRes.Content.ReadFromJsonAsync<HouseholdInviteResultDto>();
        Assert.NotNull(invite);
        Assert.False(string.IsNullOrWhiteSpace(invite!.Token));

        // 4. Register Bob
        var bobClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var bobReg = await bobClient.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("Bob Partner", "bob_partner@example.com", "Password1234!", "USD", "everyday-finances"));
        Assert.Equal(HttpStatusCode.OK, bobReg.StatusCode);

        // 5. Bob joins Alice's household using the invite token
        var joinRes = await bobClient.PostAsJsonAsync("/api/v1/household/join", new HouseholdJoinRequest(invite.Token));
        Assert.Equal(HttpStatusCode.OK, joinRes.StatusCode);

        // 6. Check Bob's household status
        var bobWs = await bobClient.GetFromJsonAsync<HouseholdWorkspaceDto>("/api/v1/household");
        Assert.NotNull(bobWs);
        Assert.Equal("Partner", bobWs!.MyRole);
        Assert.Equal(2, bobWs.Members.Count);

        // 7. Joint ledger transparency:
        // Bob creates a transaction on the joint account
        var aliceAccounts = await aliceClient.GetFromJsonAsync<AccountDto[]>("/api/v1/accounts");
        Assert.NotEmpty(aliceAccounts!);
        var jointAccount = aliceAccounts![0];

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var bobTxRes = await bobClient.PostAsJsonAsync("/api/v1/transactions", new TransactionWriteRequest(
            today,
            "Expense",
            jointAccount.Id,
            null,
            45.00m,
            0m,
            "USD",
            null,
            null,
            null,
            null,
            null,
            "Weekly Groceries by Bob",
            "Trader Joe's",
            "Joint household dinner groceries",
            "groceries,household",
            false,
            false));
        Assert.Equal(HttpStatusCode.OK, bobTxRes.StatusCode);

        // 8. Alice immediately sees Bob's transaction on the joint ledger
        var aliceTxs = await aliceClient.GetFromJsonAsync<TransactionDto[]>("/api/v1/transactions");
        Assert.NotNull(aliceTxs);
        var foundGroceries = Assert.Single(aliceTxs!, t => t.Description == "Weekly Groceries by Bob");
        Assert.Equal(45.00m, foundGroceries.Amount.Major);

        // 9. Alice removes Bob from the household
        var bobMember = bobWs.Members.First(m => m.Email == "bob_partner@example.com");
        var removeRes = await aliceClient.DeleteAsync($"/api/v1/household/members/{bobMember.UserId}");
        Assert.Equal(HttpStatusCode.OK, removeRes.StatusCode);

        // 10. Bob is no longer in Alice's household and reverts to his independent ledger
        var bobTxsAfterLeave = await bobClient.GetFromJsonAsync<TransactionDto[]>("/api/v1/transactions");
        Assert.NotNull(bobTxsAfterLeave);
        Assert.DoesNotContain(bobTxsAfterLeave!, t => t.Description == "Weekly Groceries by Bob");
    }
}
