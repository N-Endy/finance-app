using System.Net;
using System.Net.Http.Json;
using FinanceOS.Application.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinanceOS.Api.Tests;

public class ApiFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiFlowTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task Setup_login_dashboard_and_explain_net_worth()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var setup = await client.PostAsJsonAsync("/api/v1/auth/setup", new SetupRequest("Nnamdi", "nnamdi@local", "correct-horse-battery"));
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);

        var dashboard = await client.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard");
        Assert.NotNull(dashboard);
        Assert.Equal("UNKNOWN", dashboard!.NetWorth.Formatted);
        Assert.Contains("UNKNOWN", dashboard.SpendableSentence + dashboard.HousingSentence + string.Join(' ', dashboard.Unknowns));

        var explain = await client.GetFromJsonAsync<ExplainDto>("/api/v1/explain/net-worth");
        Assert.NotNull(explain);
        Assert.Contains("UNKNOWN", explain!.Sentence);
        Assert.Contains(explain.Lines, line => line.Label.Contains("Rotating") || line.Note?.Contains("EXPECTED") == true);
    }

    [Fact]
    public async Task Failed_login_is_a_generic_bad_request()
    {
        var client = _factory.CreateClient();
        var failed = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("nobody@local", "wrong-password-here"));
        Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
    }
}
