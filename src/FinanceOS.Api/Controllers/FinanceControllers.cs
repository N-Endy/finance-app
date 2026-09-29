using System.Security.Claims;
using FinanceOS.Application.Contracts;
using FinanceOS.Domain;
using FinanceOS.Domain.Entities;
using FinanceOS.Domain.Services;
using FinanceOS.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceOS.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[ServiceFilter(typeof(DomainExceptionFilter))]
public sealed class AuthController(FinanceOsService finance) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<AuthStatusDto>> Me(CancellationToken ct) =>
        Ok(await finance.StatusAsync(User.Identity?.IsAuthenticated == true, ct));

    [HttpPost("setup")]
    public async Task<ActionResult<AuthStatusDto>> Setup([FromBody] SetupRequest request, CancellationToken ct)
    {
        var owner = await finance.SetupAsync(request, ct);
        await SignIn(owner);
        return Ok(new AuthStatusDto(false, true, owner.DisplayName));
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthStatusDto>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var owner = await finance.LoginAsync(request, ct);
        await SignIn(owner);
        return Ok(new AuthStatusDto(false, true, owner.DisplayName));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok();
    }

    private async Task SignIn(Owner owner)
    {
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, owner.Id.ToString()),
            new Claim(ClaimTypes.Name, owner.DisplayName),
            new Claim(ClaimTypes.Email, owner.Email)
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}

[ApiController]
[Authorize]
[Route("api/v1")]
[ServiceFilter(typeof(DomainExceptionFilter))]
public sealed class FinanceController(FinanceOsService finance) : ControllerBase
{
    [HttpGet("dashboard")]
    public Task<DashboardDto> Dashboard(CancellationToken ct) => finance.DashboardAsync(ct);

    [HttpGet("explain/{metric}")]
    public Task<ExplainDto> Explain(string metric, CancellationToken ct) => finance.ExplainAsync(metric, ct);

    [HttpGet("accounts")]
    public Task<IReadOnlyList<AccountDto>> Accounts(CancellationToken ct) => finance.AccountsAsync(ct);

    [HttpGet("accounts/{id:guid}/reconciliation")]
    public Task<ReconciliationResult> Reconciliation(Guid id, CancellationToken ct) => finance.AccountReconciliationAsync(id, ct);

    [HttpPost("accounts/{id:guid}/snapshots")]
    public async Task<IActionResult> Snapshot(Guid id, [FromBody] SnapshotRequest request, CancellationToken ct)
    {
        await finance.AddSnapshotAsync(id, request, ct);
        return Ok();
    }

    [HttpPost("accounts/{id:guid}/assignments")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignmentRequest request, CancellationToken ct)
    {
        await finance.AssignAsync(id, request, ct);
        return Ok();
    }

    [HttpGet("envelopes")]
    public Task<IReadOnlyList<EnvelopeDto>> Envelopes(CancellationToken ct) => finance.EnvelopesAsync(ct);

    [HttpGet("transactions")]
    public Task<IReadOnlyList<TransactionDto>> Transactions(
        [FromQuery] string? account, [FromQuery] string? category, [FromQuery] bool? business, [FromQuery] bool? betting,
        CancellationToken ct) =>
        finance.TransactionsAsync(account, category, business, betting, ct);

    [HttpPost("transactions/preview")]
    public Task<PreviewDto> Preview([FromBody] QuickPreviewRequest request, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(request.Text)
            ? finance.PreviewDetailedAsync(request.Detailed!, ct)
            : finance.PreviewQuickAsync(request.Text, ct);

    [HttpPost("transactions")]
    public Task<TransactionDto> Create([FromBody] TransactionWriteRequest request, CancellationToken ct) =>
        finance.CreateTransactionAsync(request, ct);

    [HttpPost("transactions/{id:guid}/void")]
    public async Task<IActionResult> Void(Guid id, CancellationToken ct)
    {
        await finance.VoidTransactionAsync(id, ct);
        return Ok();
    }

    [HttpGet("allocation-plans/{source}")]
    public Task<IReadOnlyList<AllocationLineDto>> Plan(string source, CancellationToken ct) =>
        finance.AllocationPlanAsync(source, ct);

    [HttpPost("income-receipts/{source}/preview")]
    public Task<IncomePreviewDto> IncomePreview(string source, [FromBody] DateRequest request, CancellationToken ct) =>
        finance.PreviewIncomeAsync(source, request.Date, ct);

    [HttpPost("income-receipts/{source}/confirm")]
    public async Task<IActionResult> IncomeConfirm(string source, [FromBody] DateRequest request, CancellationToken ct)
    {
        await finance.ConfirmIncomeAsync(source, request.Date, ct);
        return Ok();
    }

    [HttpPost("actual-charges")]
    public async Task<IActionResult> Charge([FromBody] ChargeRequest request, CancellationToken ct)
    {
        await finance.RecordActualChargeAsync(request.Key, request.Amount, request.AsOf, ct);
        return Ok();
    }

    [HttpGet("money-map")]
    public Task<IReadOnlyList<MoneyMapNodeDto>> Map(CancellationToken ct) => finance.MoneyMapAsync(ct);

    [HttpGet("goals")]
    public Task<IReadOnlyList<GoalDto>> Goals(CancellationToken ct) => finance.GoalsAsync(ct);

    [HttpPut("goals/{id:guid}")]
    public async Task<IActionResult> UpdateGoal(Guid id, [FromBody] GoalWriteRequest request, CancellationToken ct)
    {
        await finance.UpdateGoalAsync(id, request.Target, request.Monthly, ct);
        return Ok();
    }

    [HttpPost("goals/housing/convert-to-next-rent")]
    public async Task<IActionResult> ConvertHousing(CancellationToken ct)
    {
        await finance.ConvertHousingToNextRentAsync(ct);
        return Ok();
    }

    [HttpGet("budget")]
    public Task<IReadOnlyList<BudgetItemDto>> Budget(CancellationToken ct) => finance.BudgetAsync(ct);

    [HttpPut("budget/{category}")]
    public async Task<IActionResult> UpdateBudget(string category, [FromBody] BudgetLineWriteRequest request, CancellationToken ct)
    {
        await finance.UpdateBudgetLineAsync(Uri.UnescapeDataString(category), request.Amount, ct);
        return Ok();
    }

    [HttpGet("holdings")]
    public Task<IReadOnlyList<HoldingDto>> Holdings(CancellationToken ct) => finance.HoldingsAsync(ct);

    [HttpPost("holdings/{id:guid}")]
    public async Task<IActionResult> UpdateHolding(Guid id, [FromBody] HoldingUpdateRequest request, CancellationToken ct)
    {
        await finance.UpdateHoldingAsync(id, request.Amount, request.Provenance, request.AsOf, ct);
        return Ok();
    }

    [HttpGet("exchange-rates")]
    public Task<IReadOnlyList<ExchangeRateDto>> Rates(CancellationToken ct) => finance.ExchangeRatesAsync(ct);

    [HttpPost("exchange-rates")]
    public async Task<IActionResult> AddRate([FromBody] ExchangeRateWriteRequest request, CancellationToken ct)
    {
        await finance.AddExchangeRateAsync(request.From, request.To, request.Rate, request.AsOf, request.Source, ct);
        return Ok();
    }

    [HttpPost("exchange-rates/today")]
    public Task<ExchangeRateDto> EnsureTodaysRate(CancellationToken ct) => finance.EnsureTodaysUsdNgnAsync(ct);

    [HttpGet("pension")]
    public Task<PensionDto> Pension(CancellationToken ct) => finance.PensionAsync(ct);

    [HttpPut("pension")]
    public async Task<IActionResult> UpdatePension([FromBody] PensionWriteRequest request, CancellationToken ct)
    {
        await finance.UpdatePensionAsync(request.Balance, request.Employee, request.Employer, request.RetirementAge, ct);
        return Ok();
    }

    [HttpGet("retirement")]
    public Task<RetirementDto> Retirement(CancellationToken ct) => finance.RetirementAsync(ct);

    [HttpPut("retirement")]
    public async Task<IActionResult> UpdateRetirement([FromBody] RetirementAssumption request, CancellationToken ct)
    {
        await finance.UpdateRetirementAsync(request, ct);
        return Ok();
    }

    [HttpGet("businesses")]
    public Task<IReadOnlyList<BusinessDto>> Businesses(CancellationToken ct) => finance.BusinessesAsync(ct);

    [HttpGet("businesses/{slug}")]
    public Task<BusinessDto> BusinessBySlug(string slug, CancellationToken ct) => finance.BusinessBySlugAsync(slug, ct);

    [HttpPost("businesses")]
    public Task<BusinessDto> CreateBusiness([FromBody] BusinessWriteRequest request, CancellationToken ct) =>
        finance.CreateBusinessAsync(request, ct);

    [HttpPut("businesses/{slug}/split")]
    public async Task<IActionResult> SplitBusiness(string slug, [FromBody] SplitRequest request, CancellationToken ct)
    {
        await finance.UpdateBusinessSplitAsync(slug, request.ReinvestPercent, request.PersonalPercent, ct);
        return Ok();
    }

    [HttpPost("businesses/{slug}/transactions")]
    public Task<BusinessDto> LogBusinessTransaction(string slug, [FromBody] BusinessTransactionRequest request, CancellationToken ct) =>
        finance.LogBusinessTransactionAsync(slug, request, ct);

    [HttpPut("businesses/{slug}/baseline-line")]
    public Task<BusinessDto> UpdateBusinessBaselineLine(string slug, [FromBody] BusinessBaselineLineRequest request, CancellationToken ct) =>
        finance.UpdateBusinessBaselineLineAsync(slug, request, ct);

    [HttpPut("businesses/{slug}/baseline")]
    public Task<BusinessDto> UpdateBusinessBaseline(string slug, [FromBody] List<BusinessBaselineLineRequest> request, CancellationToken ct) =>
        finance.UpdateBusinessBaselineAsync(slug, request, ct);

    [HttpGet("business/matchpredictor")]
    public Task<BusinessDto> Business(CancellationToken ct) => finance.BusinessAsync(ct);

    [HttpPut("business/matchpredictor/split")]
    public async Task<IActionResult> Split([FromBody] SplitRequest request, CancellationToken ct)
    {
        await finance.UpdateBusinessSplitAsync(request.ReinvestPercent, request.PersonalPercent, ct);
        return Ok();
    }

    [HttpGet("liabilities")]
    public Task<IReadOnlyList<LiabilityDto>> Liabilities(CancellationToken ct) => finance.LiabilitiesAsync(ct);

    [HttpPost("liabilities")]
    public Task<LiabilityDto> CreateLiability([FromBody] LiabilityWriteRequest request, CancellationToken ct) =>
        finance.CreateLiabilityAsync(request, ct);

    [HttpPut("liabilities/{id:guid}")]
    public async Task<IActionResult> UpdateLiability(Guid id, [FromBody] LiabilityBalanceUpdateRequest request, CancellationToken ct)
    {
        await finance.UpdateLiabilityAsync(id, request.Balance, ct);
        return Ok();
    }

    [HttpGet("counterparty-loans")]
    public Task<IReadOnlyList<CounterpartyLoanDto>> CounterpartyLoans(CancellationToken ct) => finance.CounterpartyLoansAsync(ct);

    [HttpPost("counterparty-loans")]
    public Task<CounterpartyLoanDto> CreateCounterpartyLoan([FromBody] CounterpartyLoanWriteRequest request, CancellationToken ct) =>
        finance.CreateCounterpartyLoanAsync(request, ct);

    [HttpPut("counterparty-loans/{id:guid}")]
    public async Task<IActionResult> UpdateCounterpartyLoan(Guid id, [FromBody] CounterpartyLoanUpdateRequest request, CancellationToken ct)
    {
        await finance.UpdateCounterpartyLoanAsync(id, request.BalanceRemaining, request.Status, ct);
        return Ok();
    }

    [HttpGet("actions")]
    public Task<IReadOnlyList<ActionDto>> Actions(CancellationToken ct) => finance.ActionsAsync(ct);

    [HttpPost("actions/{id:guid}/complete")]
    public Task<IActionResult> Complete(Guid id, CancellationToken ct) => Resolve(id, ActionState.Completed, ct);

    [HttpPost("actions/{id:guid}/skip")]
    public Task<IActionResult> Skip(Guid id, CancellationToken ct) => Resolve(id, ActionState.Skipped, ct);

    [HttpPost("actions/{id:guid}/snooze")]
    public Task<IActionResult> Snooze(Guid id, CancellationToken ct) => Resolve(id, ActionState.Snoozed, ct);

    [HttpGet("calendar")]
    public Task<IReadOnlyList<CalendarItemDto>> Calendar([FromQuery] int year, [FromQuery] int month, CancellationToken ct) =>
        finance.CalendarAsync(year, month, ct);

    [HttpGet("alerts")]
    public Task<IReadOnlyList<AlertDto>> Alerts(CancellationToken ct) => finance.AlertsAsync(ct);

    [HttpGet("subscriptions")]
    public Task<IReadOnlyList<SubscriptionDto>> Subscriptions(CancellationToken ct) => finance.SubscriptionsAsync(ct);

    [HttpPost("subscriptions")]
    public async Task<IActionResult> UpsertSubscription([FromBody] Subscription row, CancellationToken ct)
    {
        await finance.UpsertSubscriptionAsync(row, ct);
        return Ok();
    }

    [HttpGet("family-support")]
    public Task<IReadOnlyList<FamilySupportDto>> Family(CancellationToken ct) => finance.FamilyAsync(ct);

    [HttpPut("family-support/{id:guid}")]
    public async Task<IActionResult> FamilyLine(Guid id, [FromBody] FamilyLineWriteRequest request, CancellationToken ct)
    {
        await finance.UpdateFamilyRecipientAsync(id, request.Amount, request.Purpose, ct);
        return Ok();
    }

    [HttpGet("rules")]
    public Task<IReadOnlyList<RuleDto>> Rules(CancellationToken ct) => finance.RulesAsync(ct);

    [HttpGet("violations")]
    public Task<IReadOnlyList<ViolationDto>> Violations(CancellationToken ct) => finance.ViolationsAsync(ct);

    [HttpGet("recurring")]
    public Task<IReadOnlyList<RecurringItemDto>> Recurring(CancellationToken ct) => finance.RecurringAsync(ct);

    [HttpGet("settings")]
    public Task<SettingsDto> Settings(CancellationToken ct) => finance.SettingsAsync(ct);

    [HttpPut("settings")]
    public async Task<IActionResult> Settings([FromBody] UpdateSettingsRequest request, CancellationToken ct)
    {
        await finance.UpdateSettingsAsync(request, ct);
        return Ok();
    }

    [HttpGet("reports/{name}")]
    public Task<ReportDto> Report(string name, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        finance.ReportAsync(name, from, to, ct);

    [HttpGet("exports/{format}")]
    public async Task<IActionResult> Export(string format, CancellationToken ct)
    {
        var file = await finance.ExportAsync(format, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("imports/csv")]
    public async Task<ActionResult<object>> ImportCsv([FromBody] CsvImportRequest request, CancellationToken ct) =>
        Ok(new { imported = await finance.ImportCsvAsync(request.Csv, ct) });

    [HttpPost("imports/xlsx")]
    public async Task<ActionResult<object>> ImportXlsx([FromBody] CsvImportRequest request, CancellationToken ct) =>
        Ok(new { imported = await finance.ImportCsvAsync(request.Csv, ct) });

    [HttpGet("spending-velocity")]
    public Task<SpendingVelocityDto> SpendingVelocity(CancellationToken ct) => finance.GetSpendingVelocityAsync(ct);

    [HttpPost("statements/parse")]
    public Task<StatementParseResultDto> ParseStatement([FromBody] StatementParseRequest request, CancellationToken ct) =>
        finance.ParseStatementAsync(request, ct);

    [HttpPost("statements/commit")]
    public async Task<ActionResult<object>> CommitStatement([FromBody] StatementCommitRequest request, CancellationToken ct) =>
        Ok(new { committed = await finance.CommitStatementAsync(request, ct) });

    [HttpGet("fixed-assets")]
    public Task<IReadOnlyList<FixedAssetDto>> FixedAssets(CancellationToken ct) => finance.FixedAssetsAsync(ct);

    [HttpPost("fixed-assets")]
    public Task<FixedAssetDto> CreateFixedAsset([FromBody] FixedAssetWriteRequest request, CancellationToken ct) =>
        finance.CreateFixedAssetAsync(request, ct);

    [HttpPut("fixed-assets/{id:guid}")]
    public async Task<IActionResult> UpdateFixedAsset(Guid id, [FromBody] FixedAssetWriteRequest request, CancellationToken ct)
    {
        await finance.UpdateFixedAssetAsync(id, request, ct);
        return Ok();
    }

    [HttpPut("holdings/{slug}/valuation")]
    public Task<HoldingDto> UpdateHoldingValuation(string slug, [FromBody] HoldingValuationUpdateRequest request, CancellationToken ct) =>
        finance.UpdateHoldingValuationAsync(slug, request, ct);

    [HttpPost("budget/rebalance")]
    public async Task<IActionResult> RebalanceBudget([FromBody] EnvelopeRebalanceRequest request, CancellationToken ct)
    {
        await finance.RebalanceEnvelopesAsync(request, ct);
        return Ok();
    }

    [HttpDelete("data")]
    public async Task<IActionResult> DeleteAll(CancellationToken ct)
    {
        await finance.DeleteAllDataAsync(ct);
        return Ok();
    }

    [HttpPost("assistant")]
    [EnableRateLimiting("assistant")]
    public Task<AssistantResponseDto> Ask([FromBody] AssistantRequest request, CancellationToken ct) =>
        finance.AskAsync(request.Message, ct);

    private async Task<IActionResult> Resolve(Guid id, ActionState state, CancellationToken ct)
    {
        await finance.ResolveActionAsync(id, state, ct);
        return Ok();
    }
}

public sealed record QuickPreviewRequest(string? Text, TransactionWriteRequest? Detailed);
public sealed record DateRequest(DateOnly Date);
public sealed record ChargeRequest(string Key, decimal Amount, DateOnly AsOf);
public sealed record HoldingUpdateRequest(decimal Amount, string Provenance, DateOnly AsOf);
public sealed record ExchangeRateWriteRequest(string From, string To, decimal Rate, DateOnly AsOf, string Source);
public sealed record PensionWriteRequest(decimal? Balance, decimal? Employee, decimal? Employer, int? RetirementAge);
public sealed record SplitRequest(int ReinvestPercent, int PersonalPercent);
public sealed record FamilyLineWriteRequest(decimal Amount, string Purpose);
public sealed record BudgetLineWriteRequest(decimal Amount);
public sealed record GoalWriteRequest(decimal Target, decimal Monthly);
public sealed record CsvImportRequest(string Csv);
public sealed record LiabilityBalanceUpdateRequest(decimal Balance);
public sealed record CounterpartyLoanUpdateRequest(decimal BalanceRemaining, string Status);
