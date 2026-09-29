using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FinanceOS.Application.Contracts;
using FinanceOS.Domain;
using FinanceOS.Domain.Entities;
using FinanceOS.Domain.Services;
using FinanceOS.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinanceOS.Infrastructure.Services;

public sealed class FinanceOsService(FinanceDbContext db, IPasswordHasher<Owner> passwords, OpenAiLedgerClient llm, FxRateClient fxRates)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public async Task<Owner?> GetOwnerAsync(CancellationToken ct) =>
        await db.Owners.SingleOrDefaultAsync(ct);

    public async Task<AuthStatusDto> StatusAsync(bool authenticated, CancellationToken ct)
    {
        var owner = await GetOwnerAsync(ct);
        return new AuthStatusDto(owner is null, authenticated, owner?.DisplayName);
    }

    public async Task<Owner> SetupAsync(SetupRequest request, CancellationToken ct)
    {
        if (await db.Owners.AnyAsync(ct))
        {
            throw new DomainException("This system already has an owner.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 10)
        {
            throw new DomainException("Choose a password of at least 10 characters.");
        }

        var owner = new Owner
        {
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? "Nnamdi" : request.DisplayName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant()
        };
        owner.PasswordHash = passwords.HashPassword(owner, request.Password);
        db.Owners.Add(owner);
        await db.SaveChangesAsync(ct);
        await PlanSeeder.SeedAsync(db, owner, ct);
        await AuditAsync(owner.Id, "setup", "Owner", owner.Id, "Owner created and plan snapshot seeded.", ct);
        return owner;
    }

    public async Task<Owner> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var owner = await db.Owners.SingleOrDefaultAsync(x => x.Email == request.Email.Trim().ToLowerInvariant(), ct)
                    ?? throw new DomainException("Email or password is wrong.");
        var result = passwords.VerifyHashedPassword(owner, owner.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            throw new DomainException("Email or password is wrong.");
        }

        return owner;
    }

    public async Task<DashboardDto> DashboardAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var today = MoneyMapping.Today();
        var (start, end) = MoneyMapping.MonthBounds(today);
        var netWorth = await BuildNetWorthAsync(owner, ct);
        var spendable = await BuildSpendableAsync(owner, ct);
        var month = await MonthTotalsAsync(owner.Id, start, end, ct);
        var emergency = await HoldingMoneyAsync(owner.Id, "emergency", ct);
        var housing = await HousingMoneyAsync(owner, ct);
        var actions = await GenerateActionsAsync(owner, today, ct);
        var unknowns = await UnknownFactsAsync(owner, ct);

        var income = month.Income.Minor ?? 0;
        var saved = (month.Savings.Minor ?? 0) + (month.Investments.Minor ?? 0);
        string? savingsRate = income <= 0
            ? "Savings rate is UNKNOWN this month because no income has been recorded yet."
            : $"This month you directed {Money.FromMajor(saved / 100m, Currency.Ngn)} of {Money.FromMajor(income / 100m, Currency.Ngn)} toward savings and investments.";

        return new DashboardDto(
            netWorth.Result,
            await LiquidCashAsync(owner, ct),
            await InvestmentsMoneyAsync(owner, ct),
            emergency,
            emergency.Minor is null
                ? "Emergency fund is last-known only. Verify the current Cowrywise Emergency balance."
                : $"Emergency fund is {emergency.Formatted}. The provisional target is ₦2,400,000 until post-move essential expenses exist.",
            housing.Amount,
            housing.Sentence,
            month.Income,
            month.Expenses,
            month.Savings,
            month.Investments,
            savingsRate,
            spendable.Amount,
            spendable.Sentence,
            month,
            actions.Select(a => a.Title).Take(8).ToList(),
            unknowns);
    }

    public async Task<ExplainDto> ExplainAsync(string metric, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        return metric.ToLowerInvariant() switch
        {
            "net-worth" or "networth" => await BuildNetWorthAsync(owner, ct),
            "spendable" => (await BuildSpendableAsync(owner, ct)).Explain,
            "housing" or "housing-gap" => (await HousingMoneyAsync(owner, ct)).Explain,
            "savings-rate" => await SavingsRateExplainAsync(owner, ct),
            var name when name.StartsWith("budget-", StringComparison.Ordinal) =>
                await BudgetExplainAsync(owner, name["budget-".Length..], ct),
            _ => throw new DomainException($"No explanation is registered for '{metric}'.")
        };
    }

    public async Task<IReadOnlyList<AccountDto>> AccountsAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var accounts = await db.Accounts.Where(a => a.OwnerId == owner.Id && a.IsActive).OrderBy(a => a.Name).ToListAsync(ct);
        var result = new List<AccountDto>();
        foreach (var account in accounts)
        {
            var recon = await ReconcileAsync(account, ct);
            var latest = await LatestSnapshotAsync(account.Id, ct);
            var (health, sentence) = await HealthAsync(owner, account, recon, latest, ct);
            result.Add(new AccountDto(
                account.Id, account.Slug, account.Name, account.Institution, account.Role.ToString(),
                account.Job, account.DoNotPutHere, MoneyMapping.HealthName(health), sentence,
                latest is null
                    ? MoneyDto.Unknown($"Enter the current {account.Name} balance.")
                    : MoneyDto.Of(latest.AmountMinor, latest.Currency, latest.Provenance, latest.AsOf),
                recon.Status.ToString()));
        }

        return result;
    }

    public async Task<ReconciliationResult> AccountReconciliationAsync(Guid accountId, CancellationToken ct)
    {
        var account = await db.Accounts.SingleAsync(a => a.Id == accountId, ct);
        return await ReconcileAsync(account, ct);
    }

    public async Task AddSnapshotAsync(Guid accountId, SnapshotRequest request, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var account = await db.Accounts.SingleAsync(a => a.Id == accountId && a.OwnerId == owner.Id, ct);
        var provenance = Enum.Parse<Provenance>(request.Provenance, true);
        var source = SnapshotSource(request.Source);
        db.BalanceSnapshots.Add(new BalanceSnapshot
        {
            AccountId = account.Id,
            AmountMinor = Money.FromMajor(request.Amount, MoneyMapping.ParseCurrency(request.Currency)).MinorUnits,
            Currency = MoneyMapping.ParseCurrency(request.Currency),
            AsOf = request.AsOf,
            Provenance = provenance,
            Source = source,
            Notes = request.Notes
        });
        await AuditAsync(owner.Id, "snapshot", "Account", account.Id, $"{source} balance {request.Amount} recorded as {provenance}.", ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignAsync(Guid accountId, AssignmentRequest request, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var account = await db.Accounts.SingleAsync(a => a.Id == accountId && a.OwnerId == owner.Id, ct);
        var latest = await LatestSnapshotAsync(account.Id, ct);
        if (latest is null)
        {
            throw new DomainException($"Cannot assign money in {account.Name} until a balance is entered.");
        }

        var existing = await db.Assignments.Where(a => a.AccountId == account.Id).SumAsync(a => a.AmountMinor, ct);
        var next = Money.FromMajor(request.Amount, account.Currency).MinorUnits;
        if (existing + next > latest.AmountMinor)
        {
            throw new DomainException("Assignments cannot exceed the recorded account balance. Unassigned remainder stays visible as money with no job.");
        }

        db.Assignments.Add(new EnvelopeAssignment
        {
            AccountId = account.Id,
            EnvelopeId = request.EnvelopeId,
            AmountMinor = next,
            Currency = account.Currency,
            AsOf = request.AsOf,
            Notes = request.Notes
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TransactionDto>> TransactionsAsync(string? account, string? category, bool? business, bool? betting, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var query = db.Transactions.Include(t => t.Account).Include(t => t.Category)
            .Where(t => t.OwnerId == owner.Id && !t.IsVoided);
        if (account is not null)
        {
            query = query.Where(t => t.Account!.Slug == account);
        }

        if (category is not null)
        {
            query = query.Where(t => t.Category!.Slug == category);
        }

        if (business is true) query = query.Where(t => t.IsBusiness);
        if (betting is true) query = query.Where(t => t.Category!.IsBetting);

        var rows = await query.OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAtUtc).Take(500).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<PreviewDto> PreviewQuickAsync(string text, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var accounts = await db.Accounts.Where(a => a.OwnerId == owner.Id).ToListAsync(ct);
        var hints = accounts.ToDictionary(a => a.Slug, a => a.Slug, StringComparer.OrdinalIgnoreCase);
        foreach (var row in accounts)
        {
            hints.TryAdd(row.Name.ToLowerInvariant(), row.Slug);
        }

        var draft = QuickEntryParser.Parse(text, MoneyMapping.Today(), hints);
        if (draft.IsAmbiguous || draft.AmountMinor is null || draft.Type is null || draft.AccountHint is null)
        {
            return new PreviewDto(null, "This entry needs confirmation of the missing facts before it can be recorded.",
                draft.Questions, [], [], false);
        }

        var account = accounts.Single(a => a.Slug == draft.AccountHint);
        var counter = draft.CounterpartyHint is null ? null : accounts.SingleOrDefault(a => a.Slug == draft.CounterpartyHint);
        var category = await db.Categories.SingleOrDefaultAsync(c => c.OwnerId == owner.Id && c.Slug == draft.CategoryHint, ct);
        var envelope = await db.Envelopes.SingleOrDefaultAsync(e => e.OwnerId == owner.Id && e.Slug == draft.EnvelopeHint, ct);
        var proposed = new TransactionWriteRequest(
            draft.Date, draft.Type.Value.ToString(), account.Id, counter?.Id,
            draft.AmountMinor.Value / 100m, 0, "NGN", category?.Id, envelope?.Id, null, null, null,
            draft.Description, null, null, string.Join(',', draft.Tags), draft.IsBusiness, false);

        return new PreviewDto(proposed, $"Record {draft.Type} of {Money.FromMajor(draft.AmountMinor.Value / 100m, Currency.Ngn)} on {account.Name}.",
            [],
            [$"{account.Name} will move by {Money.FromMajor((draft.Type == TransactionType.Income || draft.Type == TransactionType.Deposit ? draft.AmountMinor.Value : -draft.AmountMinor.Value) / 100m, Currency.Ngn)}."],
            category is null ? [] : [$"{category.Name} will include this amount."],
            true);
    }

    public async Task<PreviewDto> PreviewDetailedAsync(TransactionWriteRequest request, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var account = await db.Accounts.SingleAsync(a => a.Id == request.AccountId && a.OwnerId == owner.Id, ct);
        var questions = new List<string>();
        if (request.Amount == 0) questions.Add("Amount is 0. Confirm this is intentional.");
        if (string.Equals(request.Type, "Transfer", StringComparison.OrdinalIgnoreCase) && request.CounterpartyAccountId is null)
        {
            questions.Add("Which account is the other side of this transfer?");
        }

        return new PreviewDto(request,
            $"Record {request.Type} of {Money.FromMajor(request.Amount, MoneyMapping.ParseCurrency(request.Currency))} on {account.Name}.",
            questions, [$"{account.Name} will change."], [], questions.Count == 0);
    }

    public async Task<TransactionDto> CreateTransactionAsync(TransactionWriteRequest request, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var type = MoneyMapping.ParseType(request.Type);
        if (type == TransactionType.Transfer && request.CounterpartyAccountId is null)
        {
            throw new DomainException("A transfer needs both accounts. Transfers are not expenses.");
        }

        var account = await db.Accounts.SingleAsync(a => a.Id == request.AccountId, ct);
        var currency = MoneyMapping.ParseCurrency(request.Currency);
        var amount = Money.FromMajor(request.Amount, currency);
        var fee = Money.FromMajor(request.Fee, currency);
        var category = request.CategoryId is null ? null : await db.Categories.SingleAsync(c => c.Id == request.CategoryId, ct);
        var tx = new LedgerTransaction
        {
            OwnerId = owner.Id,
            Date = request.Date,
            Type = type,
            AccountId = request.AccountId,
            CounterpartyAccountId = request.CounterpartyAccountId,
            AmountMinor = amount.MinorUnits,
            FeeMinor = fee.MinorUnits,
            Currency = currency,
            CategoryId = request.CategoryId,
            EnvelopeId = request.EnvelopeId,
            GoalId = request.GoalId,
            BusinessId = request.BusinessId,
            FamilyRecipientId = request.FamilyRecipientId,
            Description = request.Description,
            Merchant = request.Merchant,
            Notes = request.Notes,
            Tags = request.Tags,
            IsBusiness = request.IsBusiness || (category?.IsBusiness ?? false),
            IsRecurring = request.IsRecurring,
            IsTransfer = type == TransactionType.Transfer
        };

        var signed = type is TransactionType.Income or TransactionType.Deposit or TransactionType.Refund or TransactionType.BusinessRevenue
            ? amount.MinorUnits
            : -amount.MinorUnits;
        tx.Postings.Add(new Posting { AccountId = account.Id, AmountMinor = signed, Currency = currency, Role = "primary" });
        if (fee.MinorUnits > 0)
        {
            tx.Postings.Add(new Posting { AccountId = account.Id, AmountMinor = -fee.MinorUnits, Currency = currency, Role = "fee" });
        }

        if (type == TransactionType.Transfer && request.CounterpartyAccountId is Guid otherId)
        {
            tx.Postings.Add(new Posting { AccountId = otherId, AmountMinor = amount.MinorUnits, Currency = currency, Role = "counterparty" });
        }

        db.Transactions.Add(tx);
        foreach (var check in RuleEngine.EvaluateTransaction(tx, category?.Slug, account.Slug, null))
        {
            if (!check.Violated) continue;
            var rule = await db.Rules.SingleOrDefaultAsync(r => r.OwnerId == owner.Id && r.Code == check.Code, ct);
            if (rule is null) continue;
            db.Violations.Add(new RuleViolation
            {
                OwnerId = owner.Id,
                RuleId = rule.Id,
                Date = request.Date,
                Message = check.Message,
                TransactionId = tx.Id
            });
        }

        if (request.FamilyRecipientId is Guid recipientId)
        {
            var recipient = await db.FamilyRecipients.SingleAsync(r => r.Id == recipientId, ct);
            db.FamilySupports.Add(new FamilySupport
            {
                OwnerId = owner.Id,
                RecipientId = recipient.Id,
                TransactionId = tx.Id,
                Date = request.Date,
                AmountMinor = amount.MinorUnits,
                Currency = currency,
                Kind = recipient.IsRecurring ? FamilySupportKind.Recurring : FamilySupportKind.OneOff,
                Purpose = request.Description
            });
        }

        tx.Revisions.Add(new TransactionRevision
        {
            Action = "create",
            BeforeJson = "{}",
            AfterJson = JsonSerializer.Serialize(request)
        });
        await AuditAsync(owner.Id, "create", "Transaction", tx.Id, request.Description, ct);
        await db.SaveChangesAsync(ct);
        tx.Account = account;
        tx.Category = category;
        return ToDto(tx);
    }

    public async Task VoidTransactionAsync(Guid id, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var tx = await db.Transactions.SingleAsync(t => t.Id == id && t.OwnerId == owner.Id, ct);
        if (tx.IsVoided)
        {
            return;
        }

        var before = JsonSerializer.Serialize(new { tx.IsVoided, tx.AmountMinor, tx.Description });
        tx.IsVoided = true;
        db.Set<TransactionRevision>().Add(new TransactionRevision
        {
            TransactionId = tx.Id,
            Action = "void",
            BeforeJson = before,
            AfterJson = """{"isVoided":true}"""
        });
        await AuditAsync(owner.Id, "void", "Transaction", tx.Id, tx.Description, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<EnvelopeDto>> EnvelopesAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        return await db.Envelopes.Where(e => e.OwnerId == owner.Id && e.IsActive)
            .OrderBy(e => e.Name)
            .Select(e => new EnvelopeDto(e.Id, e.Slug, e.Name, e.Class.ToString(), e.Purpose, e.UseRule))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AllocationLineDto>> AllocationPlanAsync(string source, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var plan = await db.AllocationPlans.Include(p => p.Lines).Include(p => p.IncomeSource)
            .SingleAsync(p => p.OwnerId == owner.Id && p.IncomeSource!.Slug == source, ct);
        var income = plan.IncomeSource!.ExpectedAmountMinor;
        var allocated = plan.Lines.Where(l => l.Kind == AllocationKind.Fixed).Sum(l => l.AmountMinor ?? 0);
        if (allocated > income)
        {
            throw new DomainException($"RED ALERT. Allocations exceed {source} income.");
        }

        return plan.Lines.OrderBy(l => l.SortOrder).Select(l =>
            new AllocationLineDto(l.Id, l.SortOrder, l.Name, l.Kind.ToString(),
                l.AmountMinor is null ? null : MoneyDto.Of(l.AmountMinor.Value, l.Currency, Provenance.Plan),
                l.Rule)).ToList();
    }

    public async Task<IncomePreviewDto> PreviewIncomeAsync(string source, DateOnly date, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        if (source == "secondary")
        {
            var rise = await db.ActualCharges.SingleOrDefaultAsync(c => c.OwnerId == owner.Id && c.Key == "risevest-25", ct);
            var bamboo = await db.ActualCharges.SingleOrDefaultAsync(c => c.OwnerId == owner.Id && c.Key == "bamboo-30", ct);
            var result = SecondaryWaterfall.Apply(40_000_000, rise?.AmountMinor, bamboo?.AmountMinor);
            return new IncomePreviewDto("Secondary ₦400,000 waterfall",
                result.Lines.Select(l => new ExplainLineDto(l.Name,
                    l.AmountMinor is null ? MoneyDto.Unknown(l.Note) : MoneyDto.Of(l.AmountMinor.Value, Currency.Ngn, l.Provenance),
                    l.Note)).ToList(),
                result.RemainderMinor is null ? MoneyDto.Unknown("Enter actual Risevest and Bamboo naira charges.") : MoneyDto.Of(result.RemainderMinor.Value, Currency.Ngn, Provenance.Confirmed),
                result.Sentence + $" Dated {date:yyyy-MM-dd}. Confirming will record the income and the planned transfers. No real-world money will move.",
                result.RemainderMinor is not null);
        }

        var lines = await AllocationPlanAsync("salary", ct);
        var total = lines.Where(l => l.Amount?.Minor is not null).Sum(l => l.Amount!.Minor!.Value);
        AllocationMath.EnsureSalaryBalances(54_300_000, total);
        return new IncomePreviewDto("Salary ₦543,000",
            lines.Select(l => new ExplainLineDto(l.Name, l.Amount ?? MoneyDto.Unknown(l.Rule), l.Rule)).ToList(),
            MoneyDto.Of(54_300_000 - total, Currency.Ngn, Provenance.Plan),
            total == 54_300_000
                ? $"The ₦543,000 salary on {date:yyyy-MM-dd} is fully assigned. Confirming records the income and the planned envelope movements. No real-world money will move."
                : $"₦{((54_300_000 - total) / 100m):N2} is unallocated. It will stay visible rather than being hidden.",
            true);
    }

    public async Task ConfirmIncomeAsync(string source, DateOnly date, CancellationToken ct)
    {
        var preview = await PreviewIncomeAsync(source, date, ct);
        if (!preview.CanConfirm)
        {
            throw new DomainException(preview.Sentence);
        }

        var owner = await RequireOwner(ct);
        var income = await db.IncomeSources.SingleAsync(s => s.OwnerId == owner.Id && s.Slug == source, ct);
        await CreateTransactionAsync(new TransactionWriteRequest(
            date, nameof(TransactionType.Income), income.DestinationAccountId, null,
            income.ExpectedAmountMinor / 100m, 0, "NGN", null, null, null, null, null,
            income.Name, null, "Confirmed from income waterfall. Plan movement only.", source, false, true), ct);

        var plan = await db.AllocationPlans.Include(p => p.Lines)
            .SingleAsync(p => p.OwnerId == owner.Id && p.IncomeSourceId == income.Id, ct);
        foreach (var line in plan.Lines.Where(l => l.Kind == AllocationKind.Fixed && l.AmountMinor > 0 && l.DestinationAccountId is not null))
        {
            if (line.DestinationAccountId == income.DestinationAccountId) continue;
            await CreateTransactionAsync(new TransactionWriteRequest(
                date, nameof(TransactionType.Transfer), income.DestinationAccountId, line.DestinationAccountId,
                line.AmountMinor!.Value / 100m, 0, "NGN", null, line.EnvelopeId, line.GoalId, null, null,
                line.Name, null, "Planned allocation. Confirm the real-world transfer separately.", "allocation", false, true), ct);
        }
    }

    public async Task<IReadOnlyList<GoalDto>> GoalsAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var goals = await db.Goals.Where(g => g.OwnerId == owner.Id && g.IsActive).ToListAsync(ct);
        var list = new List<GoalDto>();
        foreach (var goal in goals)
        {
            var current = await GoalCurrentAsync(owner, goal, ct);
            var remaining = current.Minor is null ? (long?)null : goal.TargetMinor - current.Minor.Value;
            var sentence = goal.IsAspiration
                ? "₦500m+ is an aspiration."
                : current.Minor is null
                    ? $"Current progress is UNKNOWN. {current.Needed}"
                    : remaining <= 0
                        ? $"{goal.Name} has reached its recorded target on confirmed or last-known figures as labelled."
                        : $"You're {Money.FromMajor(remaining.Value / 100m, Currency.Ngn)} away from the {goal.Name} target.";
            var math = SinkingFundMath.RequiredMonthly(goal.TargetMinor, current.Minor, goal.Deadline, MoneyMapping.Today(), goal.MonthlyContributionMinor);
            list.Add(new GoalDto(goal.Id, goal.Slug, goal.Name, goal.Kind.ToString(),
                MoneyDto.Of(goal.TargetMinor, goal.Currency, Provenance.Plan),
                current, sentence, MoneyDto.Of(goal.MonthlyContributionMinor, goal.Currency, Provenance.Plan),
                goal.Deadline,
                math.RequiredMonthlyMinor is null ? null : MoneyDto.Of(math.RequiredMonthlyMinor.Value, goal.Currency, Provenance.Plan),
                math.ProjectedCompletion, goal.IsAspiration));
        }

        return list;
    }

    public async Task UpdateGoalAsync(Guid id, decimal target, decimal monthly, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var goal = await db.Goals.SingleAsync(g => g.Id == id && g.OwnerId == owner.Id, ct);
        goal.TargetMinor = Money.NgnFromMajor(target).MinorUnits;
        goal.MonthlyContributionMinor = Money.NgnFromMajor(monthly).MinorUnits;
        await db.SaveChangesAsync(ct);
    }

    public async Task ConvertHousingToNextRentAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        if (!owner.HasMoved)
        {
            throw new DomainException("Mark that you have moved in Settings before converting the housing goal to next rent.");
        }

        var housing = await db.Goals.SingleAsync(g => g.OwnerId == owner.Id && g.Slug == "housing", ct);
        var next = await db.Goals.SingleAsync(g => g.OwnerId == owner.Id && g.Slug == "next-rent", ct);
        housing.IsActive = false;
        next.ConvertedFromHousing = true;
        next.IsActive = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MoneyMapNodeDto>> MoneyMapAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var stanbic = await AccountMoneyAsync(owner.Id, "stanbic", ct);
        var opay = await AccountMoneyAsync(owner.Id, "opay", ct);
        var kuda = await AccountMoneyAsync(owner.Id, "kuda", ct);
        var emergency = await AccountMoneyAsync(owner.Id, "cowrywise-emergency", ct);
        var mmf = await AccountMoneyAsync(owner.Id, "cowrywise-mmf", ct);
        var children = await AccountMoneyAsync(owner.Id, "cowrywise-children", ct);
        var home = await AccountMoneyAsync(owner.Id, "cowrywise-home", ct);

        return
        [
            Node("salary", "₦543k Salary", "income", MoneyDto.Of(54_300_000, Currency.Ngn, Provenance.Plan), "Reliable monthly salary.", "Around the 27th.", "Spending it before it is assigned leaves bills and goals unfunded.", ["stanbic-node"]),
            Node("stanbic-node", "Stanbic", "account", stanbic, "Salary command centre / clearing account.", "Salary arrival and bills.", "Spending Stanbic as if it were free cash breaks the monthly plan.", ["opay-node", "cowrywise-node", "rotating-node", "reserve-node"]),
            Node("opay-node", "OPay", "account", opay, "Daily spending wallet.", "Food, transport, airtime, small purchases.", "Using it as an emergency or housing vault removes the spending boundary.", []),
            Node("cowrywise-node", "Cowrywise", "vault", null, "Named savings and investments.", "Emergency, MMF, children, home.", "Withdrawing for ordinary spending raids goal money.", ["emergency-node", "mmf-node", "children-node", "home-node"]),
            Node("emergency-node", "Emergency", "goal", emergency, "True emergencies only.", "After a genuine emergency.", "Using it to move, date, bet, or help family routinely is a rule break.", []),
            Node("mmf-node", "MMF", "goal", mmf, "Medium-term liquid wealth.", "When the purpose is wealth, not spending.", "Breaking it for lifestyle is an investment-rule violation.", []),
            Node("children-node", "Children", "goal", children, "Long-term child fund.", "Leave untouched.", "Spending it now steals from a locked 18-year purpose.", []),
            Node("home-node", "Home savings", "goal", home, "Existing home fund.", "Toward the move, then re-purpose.", "Spending it on daily costs delays housing.", []),
            Node("rotating-node", "Rotating savings", "expected", MoneyDto.Unknown("Enter the expected rotating payout when you know it. Same pool as ₦100k/month."), "Expected payout. Same pool as ₦100k/month.", "When the payout is received.", "Spending the monthly contribution as lifestyle removes move capital.", []),
            Node("reserve-node", "Operating reserve", "buffer", MoneyDto.Of(2_600_000, Currency.Ngn, Provenance.Plan), "Timing and small surprises.", "When a planned bill and cash timing differ.", "Draining it for lifestyle removes the buffer.", []),
            Node("secondary", "₦400k Secondary", "income", MoneyDto.Of(40_000_000, Currency.Ngn, Provenance.Plan), "Strategic, less reliable income.", "Around the 12th.", "Building lifestyle on it creates dependence on uncertain income.", ["kuda-node"]),
            Node("kuda-node", "Kuda", "account", kuda, "Secondary-income holding.", "Waterfall day.", "Sending the whole ₦400k to Stanbic mixes strategic money into lifestyle.", ["risevest-node", "bamboo-node", "housing-node", "irregular-node", "wealth-node"]),
            Node("risevest-node", "Risevest $25", "invest", MoneyDto.Unknown("Enter the actual naira charge. An FX rate is not being invented."), "Actual naira charge, not a guessed FX figure.", "When the charge is known.", "Skipping it silently leaves the waterfall incomplete.", []),
            Node("bamboo-node", "Bamboo $30", "invest", MoneyDto.Unknown("Enter the actual naira charge. An FX rate is not being invented."), "Actual naira charge, not a guessed FX figure.", "When the charge is known.", "Skipping it silently leaves the waterfall incomplete.", []),
            Node("housing-node", "Housing ₦250k", "goal", MoneyDto.Of(25_000_000, Currency.Ngn, Provenance.Plan), "Primary move capital from secondary income.", "Until the ₦3m working target is funded.", "Using emergency money instead breaks the housing rule.", []),
            Node("irregular-node", "Annual / irregular ₦30k", "goal", MoneyDto.Of(3_000_000, Currency.Ngn, Provenance.Plan), "Clothes, gifts, Christmas, travel, tech.", "When the planned irregular expense arrives.", "Spending it early empties the sinking fund.", []),
            Node("wealth-node", "Additional wealth ₦30k", "goal", MoneyDto.Of(3_000_000, Currency.Ngn, Provenance.Plan), "Extra protection / wealth.", "After housing is funded it can be redirected.", "Treating the Kuda remainder as spending money is lifestyle inflation.", [])
        ];

        static MoneyMapNodeDto Node(string id, string label, string kind, MoneyDto? amount, string purpose, string when, string ifSpent, IReadOnlyList<string> children) =>
            new(id, label, kind, amount ?? MoneyDto.Unknown("This is a group of named buckets, not a single balance."), purpose, when, ifSpent, children);
    }

    private async Task<MoneyDto> AccountMoneyAsync(Guid ownerId, string slug, CancellationToken ct)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.OwnerId == ownerId && a.Slug == slug, ct);
        if (account is null) return MoneyDto.Unknown("This account is not on the plan.");
        var snap = await LatestSnapshotAsync(account.Id, ct);
        return snap is null
            ? MoneyDto.Unknown($"Enter the current {account.Name} balance.")
            : MoneyDto.Of(snap.AmountMinor, snap.Currency, snap.Provenance, snap.AsOf);
    }

    public async Task<IReadOnlyList<BudgetItemDto>> BudgetAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var today = MoneyMapping.Today();
        var (start, end) = MoneyMapping.MonthBounds(today);
        var plan = await db.AllocationPlans.Include(p => p.Lines).ThenInclude(l => l.Envelope)
            .Include(p => p.IncomeSource)
            .SingleAsync(p => p.OwnerId == owner.Id && p.IncomeSource!.Slug == "salary", ct);
        var txs = await db.Transactions.Include(t => t.Category).Include(t => t.Envelope)
            .Where(t => t.OwnerId == owner.Id && !t.IsVoided && t.Date >= start && t.Date <= end && !t.IsTransfer && !t.IsBusiness)
            .ToListAsync(ct);

        var items = new List<BudgetItemDto>();
        foreach (var line in plan.Lines.Where(l => l.Envelope is { Class: EnvelopeClass.Spend or EnvelopeClass.Committed or EnvelopeClass.Sinking or EnvelopeClass.Business }))
        {
            var actual = txs.Where(t => t.EnvelopeId == line.EnvelopeId && t.Type is TransactionType.Expense or TransactionType.Fee)
                .Sum(t => t.AmountMinor);
            var budget = line.AmountMinor ?? 0;
            var (status, sentence, percent) = BudgetStatusCalculator.Evaluate(budget, actual, owner.WatchPercent, owner.WarningPercent, owner.OverBudgetPercent);
            items.Add(new BudgetItemDto(line.Name,
                MoneyDto.Of(budget, Currency.Ngn, Provenance.Plan),
                MoneyDto.Of(actual, Currency.Ngn, Provenance.Confirmed),
                MoneyDto.Of(budget - actual, Currency.Ngn, Provenance.Confirmed),
                percent is null ? "UNKNOWN" : $"{percent:0.#}%",
                status.ToString(),
                sentence));
        }

        var bettingActual = txs.Where(t => t.Category is { IsBetting: true }).Sum(t => t.AmountMinor);
        var betting = BudgetStatusCalculator.Evaluate(0, bettingActual, owner.WatchPercent, owner.WarningPercent, owner.OverBudgetPercent);
        items.Add(new BudgetItemDto("Betting",
            MoneyDto.Of(0, Currency.Ngn, Provenance.Plan),
            MoneyDto.Of(bettingActual, Currency.Ngn, Provenance.Confirmed),
            MoneyDto.Of(-bettingActual, Currency.Ngn, Provenance.Confirmed),
            bettingActual == 0 ? "0%" : "over",
            bettingActual == 0 ? "Normal" : "OverBudget",
            bettingActual == 0
                ? "Betting allocation is ₦0 and no betting expenses were recorded this month."
                : $"Betting activity of {Money.FromMajor(bettingActual / 100m, Currency.Ngn)} was recorded. The plan allocates ₦0."));

        return items;
    }

    public async Task UpdateBudgetLineAsync(string category, decimal amount, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var plan = await db.AllocationPlans.Include(p => p.Lines).Include(p => p.IncomeSource)
            .SingleAsync(p => p.OwnerId == owner.Id && p.IncomeSource!.Slug == "salary", ct);
        var line = plan.Lines.SingleOrDefault(l => string.Equals(l.Name, category, StringComparison.OrdinalIgnoreCase))
                   ?? throw new DomainException($"No salary allocation named {category}.");
        line.AmountMinor = Money.NgnFromMajor(amount).MinorUnits;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<HoldingDto>> HoldingsAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var rows = await db.Holdings.Where(h => h.OwnerId == owner.Id).OrderBy(h => h.Name).ToListAsync(ct);
        return rows.Select(h => new HoldingDto(
            h.Id, h.Name,
            h.Provenance == Provenance.Unknown
                ? MoneyDto.Unknown(h.StatusNote, h.Currency)
                : MoneyDto.Of(h.AmountMinor, h.Currency, h.Provenance, h.AsOf),
            h.Liquidity, h.Purpose, h.StatusNote, h.IsExpectedReceivable)).ToList();
    }

    public async Task UpdateHoldingAsync(Guid id, decimal amount, string provenance, DateOnly asOf, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var holding = await db.Holdings.SingleAsync(h => h.Id == id && h.OwnerId == owner.Id, ct);
        holding.AmountMinor = Money.FromMajor(amount, holding.Currency).MinorUnits;
        holding.Provenance = Enum.Parse<Provenance>(provenance, true);
        holding.AsOf = asOf;
        if (holding.AccountId is Guid accountId)
        {
            db.BalanceSnapshots.Add(new BalanceSnapshot
            {
                AccountId = accountId,
                AmountMinor = holding.AmountMinor,
                Currency = holding.Currency,
                AsOf = asOf,
                Provenance = holding.Provenance,
                Source = "holding-update",
                Notes = "Entered as the current holding balance."
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ExchangeRateDto>> ExchangeRatesAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        return await db.ExchangeRates.Where(r => r.OwnerId == owner.Id)
            .OrderByDescending(r => r.AsOf)
            .Select(r => new ExchangeRateDto(r.From.ToString(), r.To.ToString(), r.Rate, r.AsOf, r.Source))
            .ToListAsync(ct);
    }

    public async Task AddExchangeRateAsync(string from, string to, decimal rate, DateOnly asOf, string source, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        if (rate <= 0) throw new DomainException("An FX rate must be a positive number you actually observed.");
        db.ExchangeRates.Add(new ExchangeRate
        {
            OwnerId = owner.Id,
            From = MoneyMapping.ParseCurrency(from),
            To = MoneyMapping.ParseCurrency(to),
            Rate = rate,
            AsOf = asOf,
            Source = string.IsNullOrWhiteSpace(source) ? "manual" : source.Trim()
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<ExchangeRateDto> EnsureTodaysUsdNgnAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var today = MoneyMapping.Today();
        var existing = await TodaysUsdNgnAsync(owner.Id, today, ct);
        if (existing is not null)
        {
            return ToRateDto(existing);
        }

        var fetched = await fxRates.FetchUsdNgnAsync(ct);
        if (fetched is null)
        {
            throw new DomainException("Today's USD/NGN rate is UNKNOWN. The published feed did not return a figure.");
        }

        db.ExchangeRates.Add(new ExchangeRate
        {
            OwnerId = owner.Id,
            From = Currency.Usd,
            To = Currency.Ngn,
            Rate = fetched.Value.Rate,
            AsOf = today,
            Source = FxRateClient.SourceName
        });
        await db.SaveChangesAsync(ct);
        return new ExchangeRateDto("USD", "NGN", fetched.Value.Rate, today, FxRateClient.SourceName);
    }

    private async Task<ExchangeRate?> TodaysUsdNgnAsync(Guid ownerId, DateOnly today, CancellationToken ct)
    {
        var rows = await db.ExchangeRates
            .Where(r => r.OwnerId == ownerId && r.From == Currency.Usd && r.To == Currency.Ngn && r.AsOf == today)
            .ToListAsync(ct);
        return rows
            .OrderByDescending(r => r.Source == "manual")
            .ThenByDescending(r => r.RecordedAtUtc)
            .FirstOrDefault();
    }

    private static ExchangeRateDto ToRateDto(ExchangeRate rate) =>
        new(rate.From.ToString(), rate.To.ToString(), rate.Rate, rate.AsOf, rate.Source);

    public async Task<PensionDto> PensionAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var pension = await db.Pensions.SingleAsync(p => p.OwnerId == owner.Id, ct);
        if (pension.CurrentBalanceMinor is null)
        {
            return new PensionDto(MoneyDto.Unknown("RSA balance is unknown until a statement is entered."),
                null, null, "RSA balance is unknown.");
        }

        return new PensionDto(
            MoneyDto.Of(pension.CurrentBalanceMinor.Value, pension.Currency, pension.Provenance),
            pension.EmployeeContributionMinor is null ? null : MoneyDto.Of(pension.EmployeeContributionMinor.Value, pension.Currency, Provenance.Confirmed),
            pension.EmployerContributionMinor is null ? null : MoneyDto.Of(pension.EmployerContributionMinor.Value, pension.Currency, Provenance.Confirmed),
            pension.Provenance == Provenance.Confirmed ? "RSA figures are as you entered them." : "Last-known RSA balance.");
    }

    public async Task UpdatePensionAsync(decimal? balance, decimal? employee, decimal? employer, int? retirementAge, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var pension = await db.Pensions.SingleAsync(p => p.OwnerId == owner.Id, ct);
        pension.CurrentBalanceMinor = balance is null ? null : Money.NgnFromMajor(balance.Value).MinorUnits;
        pension.EmployeeContributionMinor = employee is null ? null : Money.NgnFromMajor(employee.Value).MinorUnits;
        pension.EmployerContributionMinor = employer is null ? null : Money.NgnFromMajor(employer.Value).MinorUnits;
        pension.RetirementAge = retirementAge;
        pension.Provenance = balance is null ? Provenance.Unknown : Provenance.Confirmed;
        await db.SaveChangesAsync(ct);
    }

    public async Task<RetirementDto> RetirementAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var assumptions = await db.RetirementAssumptions.SingleAsync(r => r.OwnerId == owner.Id, ct);
        var invested = await db.Holdings.Where(h => h.OwnerId == owner.Id && h.IncludeInNetWorth && !h.IsExpectedReceivable && h.Provenance == Provenance.Confirmed && h.Currency == Currency.Ngn)
            .SumAsync(h => (long?)h.AmountMinor, ct) ?? 0;
        var scenarios = RetirementCalculator.Project(
            assumptions.CurrentAge ?? owner.Age,
            assumptions.RetirementAge,
            invested == 0 ? null : invested,
            assumptions.InvestmentsConfirmed,
            assumptions.MonthlyContributionMinor,
            assumptions.ConservativeReturn,
            assumptions.BaseReturn,
            assumptions.AggressiveReturn,
            assumptions.Inflation);
        return new RetirementDto(scenarios[0].Sentence,
            scenarios.Select(s => new RetirementScenarioDto(s.Name, s.Sentence,
                s.FutureValueMinor is null ? null : MoneyDto.Estimate(s.FutureValueMinor.Value, Currency.Ngn, s.Sentence))).ToList());
    }

    public async Task UpdateRetirementAsync(RetirementAssumption patch, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var row = await db.RetirementAssumptions.SingleAsync(r => r.OwnerId == owner.Id, ct);
        row.CurrentAge = patch.CurrentAge;
        row.RetirementAge = patch.RetirementAge;
        row.MonthlyContributionMinor = patch.MonthlyContributionMinor;
        row.ConservativeReturn = patch.ConservativeReturn;
        row.BaseReturn = patch.BaseReturn;
        row.AggressiveReturn = patch.AggressiveReturn;
        row.Inflation = patch.Inflation;
        row.DesiredAnnualSpendingMinor = patch.DesiredAnnualSpendingMinor;
        row.WithdrawalRate = patch.WithdrawalRate;
        row.InvestmentsConfirmed = patch.InvestmentsConfirmed;
        owner.Age = patch.CurrentAge;
        await db.SaveChangesAsync(ct);
    }

    public async Task<BusinessDto> BusinessAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var business = await db.Businesses.SingleAsync(b => b.OwnerId == owner.Id && b.Slug == "matchpredictor", ct);
        var txs = await db.Transactions.Include(t => t.Category)
            .Where(t => t.OwnerId == owner.Id && !t.IsVoided && t.IsBusiness)
            .ToListAsync(ct);
        var revenue = txs.Where(t => t.Type == TransactionType.BusinessRevenue).Sum(t => t.AmountMinor);
        var expenses = txs.Where(t => t.Type is TransactionType.BusinessExpense or TransactionType.Expense).Sum(t => t.AmountMinor);
        var groups = txs.Where(t => t.Type != TransactionType.BusinessRevenue)
            .GroupBy(t => t.Category?.Name ?? "Other")
            .Select(g => new BusinessLineDto(g.Key, MoneyDto.Of(g.Sum(x => x.AmountMinor), Currency.Ngn, Provenance.Confirmed)))
            .ToList();
        if (groups.Count == 0)
        {
            groups.Add(new BusinessLineDto("Hosting", MoneyDto.Unknown("Enter the actual Railway naira charge.")));
            groups.Add(new BusinessLineDto("Domain", MoneyDto.Unknown("Enter the actual domain naira charge.")));
            groups.Add(new BusinessLineDto("Database", MoneyDto.Estimate(1_500_000, Currency.Ngn, "Workbook listed Neon at ₦15,000 as a plan estimate.")));
            groups.Add(new BusinessLineDto("AI / API", MoneyDto.Unknown("Enter the actual AI token naira charge.")));
            groups.Add(new BusinessLineDto("Revenue", MoneyDto.Of(0, Currency.Ngn, Provenance.Confirmed, null)));
        }

        return new BusinessDto(business.Name,
            MoneyDto.Of(revenue, Currency.Ngn, Provenance.Confirmed),
            expenses == 0 ? MoneyDto.Unknown("No confirmed MatchPredictor expenses have been entered yet. Plan estimates are listed separately.") : MoneyDto.Of(expenses, Currency.Ngn, Provenance.Confirmed),
            expenses == 0 && revenue == 0 ? MoneyDto.Unknown("Net result is UNKNOWN until actual costs and revenue are entered.") : MoneyDto.Of(revenue - expenses, Currency.Ngn, Provenance.Confirmed),
            business.ReinvestPercent, business.PersonalPercent, groups);
    }

    public async Task UpdateBusinessSplitAsync(int reinvest, int personal, CancellationToken ct)
    {
        if (reinvest + personal != 100) throw new DomainException("Reinvestment and personal percentages must add to 100.");
        var owner = await RequireOwner(ct);
        var business = await db.Businesses.SingleAsync(b => b.OwnerId == owner.Id && b.Slug == "matchpredictor", ct);
        business.ReinvestPercent = reinvest;
        business.PersonalPercent = personal;
        owner.BusinessReinvestPercent = reinvest;
        owner.BusinessPersonalPercent = personal;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ActionDto>> ActionsAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        return await GenerateActionsAsync(owner, MoneyMapping.Today(), ct);
    }

    public async Task ResolveActionAsync(Guid id, ActionState state, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var action = await db.Actions.SingleAsync(a => a.Id == id && a.OwnerId == owner.Id, ct);
        action.State = state;
        action.ResolvedAtUtc = DateTime.UtcNow;
        if (state == ActionState.Snoozed) action.SnoozeUntil = MoneyMapping.Today().AddDays(1);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CalendarItemDto>> CalendarAsync(int year, int month, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var items = new List<CalendarItemDto>();
        foreach (var row in await db.RecurringItems.Where(r => r.OwnerId == owner.Id && r.IsActive).ToListAsync(ct))
        {
            var day = Math.Min(row.DayOfMonth, DateTime.DaysInMonth(year, month));
            items.Add(new CalendarItemDto(new DateOnly(year, month, day), row.Name, row.Kind,
                row.AmountMinor is null ? null : MoneyDto.Of(row.AmountMinor.Value, row.Currency, Provenance.Plan)));
        }

        foreach (var sub in await db.Subscriptions.Where(s => s.OwnerId == owner.Id && s.IsActive && s.NextBillingDate >= start && s.NextBillingDate <= end).ToListAsync(ct))
        {
            items.Add(new CalendarItemDto(sub.NextBillingDate, sub.Name, "subscription",
                sub.AmountMinor == 0 ? MoneyDto.Unknown($"Enter the actual {sub.Name} charge.") : MoneyDto.Of(sub.AmountMinor, sub.Currency, Provenance.Plan)));
        }

        return items.OrderBy(i => i.Date).ToList();
    }

    public async Task<IReadOnlyList<AlertDto>> AlertsAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var alerts = new List<AlertDto>();
        foreach (var account in await db.Accounts.Where(a => a.OwnerId == owner.Id && a.IsActive).ToListAsync(ct))
        {
            var recon = await ReconcileAsync(account, ct);
            if (recon.Status == ReconciliationStatus.Unreconciled)
            {
                alerts.Add(new AlertDto("Reconciliation", recon.Sentence));
            }
        }

        var budget = await BudgetAsync(ct);
        foreach (var item in budget.Where(b => b.Status is "OverBudget" or "Warning"))
        {
            alerts.Add(new AlertDto(item.Status == "OverBudget" ? "Overspending" : "CashFlow", item.Sentence));
        }

        var today = MoneyMapping.Today();
        var (start, _) = MoneyMapping.MonthBounds(today);
        var emergencyMoved = await db.Transactions.AnyAsync(t => t.OwnerId == owner.Id && !t.IsVoided && t.Date >= start && t.Description.Contains("Emergency"), ct);
        if (today.Day >= 28 && !emergencyMoved)
        {
            alerts.Add(new AlertDto("MissedSavings", "The planned ₦50,000 emergency contribution has not been recorded this month."));
        }

        var access = await db.Accounts.SingleAsync(a => a.OwnerId == owner.Id && a.Slug == "access", ct);
        var accessSnap = await LatestSnapshotAsync(access.Id, ct);
        var due = await db.Subscriptions.Where(s => s.OwnerId == owner.Id && s.IsActive && s.PaymentAccountId == access.Id && s.NextBillingDate <= today.AddDays(7)).ToListAsync(ct);
        var dueTotal = due.Where(d => d.AmountMinor > 0 && d.Currency == Currency.Ngn).Sum(d => d.AmountMinor);
        if (due.Count > 0)
        {
            if (accessSnap is null)
            {
                alerts.Add(new AlertDto("SubscriptionFunding", $"{Money.FromMajor(dueTotal / 100m, Currency.Ngn)} is scheduled to leave Access Bank in the next 7 days. The Access balance is UNKNOWN."));
            }
            else if (accessSnap.AmountMinor < dueTotal)
            {
                alerts.Add(new AlertDto("SubscriptionFunding", $"Access needs {Money.FromMajor((dueTotal - accessSnap.AmountMinor) / 100m, Currency.Ngn)} before the next subscription cycle."));
            }
        }

        return alerts
            .OrderBy(a => a.Priority switch
            {
                "Reconciliation" => 1,
                "CashFlow" => 2,
                "Overspending" => 3,
                "MissedSavings" => 4,
                "GoalDelay" => 5,
                _ => 6
            })
            .ToList();
    }

    public async Task<IReadOnlyList<SubscriptionDto>> SubscriptionsAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        return await db.Subscriptions.Where(s => s.OwnerId == owner.Id)
            .OrderBy(s => s.NextBillingDate)
            .Select(s => new SubscriptionDto(s.Id, s.Name,
                s.AmountMinor == 0 ? MoneyDto.Unknown($"Enter the actual {s.Name} charge.") : MoneyDto.Of(s.AmountMinor, s.Currency, Provenance.Plan),
                s.Frequency.ToString(), s.PaymentAccountId, s.NextBillingDate, s.Category, s.IsBusiness, s.IsActive))
            .ToListAsync(ct);
    }

    public async Task UpsertSubscriptionAsync(Subscription row, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        row.OwnerId = owner.Id;
        if (row.Id == Guid.Empty) db.Subscriptions.Add(row);
        else db.Subscriptions.Update(row);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FamilySupportDto>> FamilyAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var rows = await db.FamilySupports.Include(f => f.Recipient)
            .Where(f => f.OwnerId == owner.Id)
            .OrderByDescending(f => f.Date)
            .ToListAsync(ct);
        var recurring = await db.FamilyRecipients.Where(r => r.OwnerId == owner.Id && r.IsRecurring).ToListAsync(ct);
        var result = recurring.Select(r => new FamilySupportDto(r.Id, r.Name, MoneyMapping.Today(),
            r.RecurringAmountMinor is null ? MoneyDto.Unknown("Set the recurring amount.") : MoneyDto.Of(r.RecurringAmountMinor.Value, Currency.Ngn, Provenance.Plan),
            "Recurring", r.Purpose)).ToList();
        result.AddRange(rows.Select(r => new FamilySupportDto(r.Id, r.Recipient!.Name, r.Date,
            MoneyDto.Of(r.AmountMinor, r.Currency, Provenance.Confirmed), r.Kind.ToString(), r.Purpose)));
        return result;
    }

    public async Task UpdateFamilyRecipientAsync(Guid id, decimal amount, string purpose, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var recipients = await db.FamilyRecipients.Where(r => r.OwnerId == owner.Id && r.IsRecurring).ToListAsync(ct);
        var row = recipients.Single(r => r.Id == id);
        row.RecurringAmountMinor = Money.NgnFromMajor(amount).MinorUnits;
        row.Purpose = purpose;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RuleDto>> RulesAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        return await db.Rules.Where(r => r.OwnerId == owner.Id)
            .Select(r => new RuleDto(r.Id, r.Code, r.Title, r.Action, r.Control, r.IsActive))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ViolationDto>> ViolationsAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        return await db.Violations.Where(v => v.OwnerId == owner.Id)
            .Join(db.Rules, v => v.RuleId, r => r.Id, (v, r) => new ViolationDto(v.Id, r.Title, v.Message, v.Date, v.IsOpen))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RecurringItemDto>> RecurringAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var rows = await db.RecurringItems.Where(r => r.OwnerId == owner.Id).ToListAsync(ct);
        return rows.Select(r => new RecurringItemDto(r.Id, r.Name, r.DayOfMonth,
            r.AmountMinor is null ? null : MoneyDto.Of(r.AmountMinor.Value, r.Currency, Provenance.Plan), r.Kind)).ToList();
    }

    public async Task<SettingsDto> SettingsAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var family = await db.FamilyRecipients.Where(r => r.OwnerId == owner.Id && r.IsRecurring).ToListAsync(ct);
        return new SettingsDto(owner.DisplayName,
            owner.OpayMonthlyAllowanceMinor is null
                ? MoneyDto.Unknown("Choose a single OPay monthly allowance inside the ₦60,000–₦80,000 band before overspend alerts fire.")
                : MoneyDto.Of(owner.OpayMonthlyAllowanceMinor.Value, Currency.Ngn, Provenance.Confirmed),
            owner.WatchPercent, owner.WarningPercent, owner.OverBudgetPercent,
            owner.BusinessReinvestPercent, owner.BusinessPersonalPercent,
            family.Select(f => new FamilyLineDto(f.Id, f.Name,
                f.RecurringAmountMinor is null ? null : MoneyDto.Of(f.RecurringAmountMinor.Value, Currency.Ngn, Provenance.Plan),
                f.Purpose, f.IsRecurring)).ToList(),
            owner.HasMoved);
    }

    public async Task UpdateSettingsAsync(UpdateSettingsRequest request, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        if (request.OpayAllowance is not null)
        {
            if (request.OpayAllowance < 60000 || request.OpayAllowance > 80000)
            {
                throw new DomainException("The starting OPay allowance band is ₦60,000–₦80,000. Pick a single number inside that band.");
            }

            owner.OpayMonthlyAllowanceMinor = Money.NgnFromMajor(request.OpayAllowance.Value).MinorUnits;
        }

        owner.WatchPercent = request.WatchPercent ?? owner.WatchPercent;
        owner.WarningPercent = request.WarningPercent ?? owner.WarningPercent;
        owner.OverBudgetPercent = request.OverBudgetPercent ?? owner.OverBudgetPercent;
        owner.BusinessReinvestPercent = request.BusinessReinvestPercent ?? owner.BusinessReinvestPercent;
        owner.BusinessPersonalPercent = request.BusinessPersonalPercent ?? owner.BusinessPersonalPercent;
        owner.HasMoved = request.HasMoved ?? owner.HasMoved;
        await db.SaveChangesAsync(ct);
    }

    public async Task RecordActualChargeAsync(string key, decimal amount, DateOnly asOf, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var existing = await db.ActualCharges.SingleOrDefaultAsync(c => c.OwnerId == owner.Id && c.Key == key, ct);
        var minor = Money.NgnFromMajor(amount).MinorUnits;
        if (existing is null)
        {
            db.ActualCharges.Add(new ActualCharge { OwnerId = owner.Id, Key = key, AmountMinor = minor, AsOf = asOf });
        }
        else
        {
            existing.AmountMinor = minor;
            existing.AsOf = asOf;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<ReportDto> ReportAsync(string name, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var txs = await db.Transactions.Include(t => t.Category).Include(t => t.Account)
            .Where(t => t.OwnerId == owner.Id && !t.IsVoided && t.Date >= from && t.Date <= to)
            .ToListAsync(ct);

        IReadOnlyList<ExplainLineDto> lines;
        string summary;
        switch (name.ToLowerInvariant())
        {
            case "spending":
                var spend = txs.Where(t => t.Type == TransactionType.Expense && !t.IsBusiness && t.Category is not { IsBetting: true } && !t.IsTransfer)
                    .GroupBy(t => t.Category?.Name ?? "Other")
                    .Select(g => new ExplainLineDto(g.Key, MoneyDto.Of(g.Sum(x => x.AmountMinor), Currency.Ngn, Provenance.Confirmed), null))
                    .ToList();
                summary = spend.Count == 0
                    ? "No personal expenses were recorded in this period."
                    : $"Personal spending in this period is {Money.FromMajor(spend.Sum(s => s.Amount.Minor ?? 0) / 100m, Currency.Ngn)}.";
                lines = spend;
                break;
            case "family":
                var family = txs.Where(t => t.Category is { IsFamily: true })
                    .Select(t => new ExplainLineDto(t.Description, MoneyDto.Of(t.AmountMinor, t.Currency, Provenance.Confirmed), t.Notes))
                    .ToList();
                summary = $"Family support recorded in this period: {Money.FromMajor(family.Sum(f => f.Amount.Minor ?? 0) / 100m, Currency.Ngn)}. One-off transfers are not turned into recurring obligations.";
                lines = family;
                break;
            case "betting":
                var bets = txs.Where(t => t.Category is { IsBetting: true }).ToList();
                var note = await db.ExternalNotes.SingleOrDefaultAsync(n => n.OwnerId == owner.Id && n.Topic == "sportybet-august", ct);
                lines = bets.Select(t => new ExplainLineDto(t.Description, MoneyDto.Of(t.AmountMinor, t.Currency, Provenance.Confirmed), t.Notes)).ToList();
                summary = bets.Count == 0
                    ? "No betting deposits were recorded in this period. Monthly allocation is ₦0."
                    : $"Betting deposits in the ledger: {Money.FromMajor(bets.Sum(b => b.AmountMinor) / 100m, Currency.Ngn)}. Net result is UNKNOWN without stakes and winnings. {(note?.Body ?? "")}";
                break;
            case "cash-flow":
                var income = txs.Where(t => t.Type is TransactionType.Income or TransactionType.BusinessRevenue).Sum(t => t.AmountMinor);
                var expense = txs.Where(t => t.Type is TransactionType.Expense or TransactionType.BusinessExpense or TransactionType.Fee).Sum(t => t.AmountMinor);
                lines =
                [
                    new ExplainLineDto("Income", MoneyDto.Of(income, Currency.Ngn, Provenance.Confirmed), "Transfers excluded."),
                    new ExplainLineDto("Expenses and fees", MoneyDto.Of(expense, Currency.Ngn, Provenance.Confirmed), "Own-account transfers excluded.")
                ];
                summary = $"Income {Money.FromMajor(income / 100m, Currency.Ngn)} minus expenses and fees {Money.FromMajor(expense / 100m, Currency.Ngn)}.";
                break;
            case "business":
                var biz = await BusinessAsync(ct);
                summary = biz.Net.Formatted;
                lines = biz.Lines.Select(l => new ExplainLineDto(l.Category, l.Amount, null)).ToList();
                break;
            case "reconciliation":
                var recLines = new List<ExplainLineDto>();
                foreach (var account in await db.Accounts.Where(a => a.OwnerId == owner.Id && a.IsActive).ToListAsync(ct))
                {
                    var recon = await ReconcileAsync(account, ct);
                    recLines.Add(new ExplainLineDto(account.Name,
                        recon.DifferenceMinor is null ? MoneyDto.Unknown(recon.Sentence) : MoneyDto.Of(recon.DifferenceMinor.Value, Currency.Ngn, Provenance.Confirmed),
                        recon.Sentence));
                }

                summary = recLines.Any(l => l.Amount.Formatted != "UNKNOWN" && l.Amount.Minor != 0)
                    ? "At least one account is UNRECONCILED."
                    : "No invented differences. Incomplete accounts stay UNKNOWN.";
                lines = recLines;
                break;
            case "net-worth":
                var nw = await BuildNetWorthAsync(owner, ct);
                return new ReportDto("net-worth", $"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}", nw.Sentence, nw.Lines);
            default:
                summary = $"Report '{name}' uses recorded transactions only between {from:yyyy-MM-dd} and {to:yyyy-MM-dd}.";
                lines = txs.Select(t => new ExplainLineDto($"{t.Date:yyyy-MM-dd} {t.Description}", MoneyDto.Of(t.AmountMinor, t.Currency, Provenance.Confirmed), t.Type.ToString())).ToList();
                break;
        }

        return new ReportDto(name, $"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}", summary, lines);
    }

    public async Task<(string FileName, string ContentType, byte[] Content)> ExportAsync(string format, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var txs = await db.Transactions.Include(t => t.Account).Include(t => t.Category)
            .Where(t => t.OwnerId == owner.Id).OrderBy(t => t.Date).ToListAsync(ct);
        if (format == "json")
        {
            var payload = new
            {
                owner.DisplayName,
                exportedAt = DateTime.UtcNow,
                transactions = txs.Select(ToDto),
                accounts = await AccountsAsync(ct),
                holdings = await HoldingsAsync(ct)
            };
            return ("financeos-export.json", "application/json", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOpts)));
        }

        var csv = new StringBuilder();
        csv.AppendLine("Date,Account,Type,Category,Description,Amount,Fee,Currency,Transfer,Business,Voided,Notes");
        foreach (var tx in txs)
        {
            csv.AppendLine(string.Join(',',
                tx.Date.ToString("yyyy-MM-dd"), Csv(tx.Account?.Name), tx.Type, Csv(tx.Category?.Name), Csv(tx.Description),
                (tx.AmountMinor / 100m).ToString(CultureInfo.InvariantCulture),
                (tx.FeeMinor / 100m).ToString(CultureInfo.InvariantCulture),
                tx.Currency, tx.IsTransfer, tx.IsBusiness, tx.IsVoided, Csv(tx.Notes)));
        }

        if (format == "xlsx")
        {
            return ("financeos-export.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                MinimalXlsx("Transactions", csv.ToString()));
        }

        return ("financeos-export.csv", "text/csv", Encoding.UTF8.GetBytes(csv.ToString()));
    }

    public async Task<int> ImportCsvAsync(string csv, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var accounts = await db.Accounts.Where(a => a.OwnerId == owner.Id).ToListAsync(ct);
        var categories = await db.Categories.Where(c => c.OwnerId == owner.Id).ToListAsync(ct);
        var lines = csv.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var imported = 0;
        foreach (var line in lines.Skip(1))
        {
            var cols = SplitCsv(line);
            if (cols.Length < 7) continue;
            var account = accounts.FirstOrDefault(a => a.Name.Equals(cols[1], StringComparison.OrdinalIgnoreCase) || a.Slug.Equals(cols[1], StringComparison.OrdinalIgnoreCase));
            if (account is null) throw new DomainException($"Import stopped. Account '{cols[1]}' is unknown. Nothing after this line was saved.");
            if (!decimal.TryParse(cols[5], CultureInfo.InvariantCulture, out var amount))
            {
                throw new DomainException($"Import stopped. Amount '{cols[5]}' is not a number.");
            }

            var category = categories.FirstOrDefault(c => c.Name.Equals(cols[3], StringComparison.OrdinalIgnoreCase) || c.Slug.Equals(cols[3], StringComparison.OrdinalIgnoreCase));
            await CreateTransactionAsync(new TransactionWriteRequest(
                DateOnly.Parse(cols[0], CultureInfo.InvariantCulture),
                cols[2], account.Id, null, amount,
                cols.Length > 6 && decimal.TryParse(cols[6], CultureInfo.InvariantCulture, out var fee) ? fee : 0,
                cols.Length > 7 ? cols[7] : "NGN", category?.Id, category?.EnvelopeId, null, null, null,
                cols[4], null, "Imported", "import", false, false), ct);
            imported++;
        }

        return imported;
    }

    public async Task DeleteAllDataAsync(CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        await PlanSeeder.ClearOwnerPlanAsync(db, owner, ct);
        owner.PlanSeeded = false;
        await PlanSeeder.SeedAsync(db, owner, ct);
        await AuditAsync(owner.Id, "reset", "Owner", owner.Id, "User requested delete and reseed.", ct);
    }

    public async Task<AssistantResponseDto> AskAsync(string message, CancellationToken ct)
    {
        var owner = await RequireOwner(ct);
        var today = MoneyMapping.Today();
        var (start, end) = MoneyMapping.MonthBounds(today);
        var month = await MonthTotalsAsync(owner.Id, start, end, ct);
        var spendable = await BuildSpendableAsync(owner, ct);
        var budget = await BudgetAsync(ct);
        var remainingSpend = budget.Where(b => b.Category is not "Betting")
            .Sum(b => Math.Max(0, b.Remaining.Minor ?? 0));
        var housing = await HousingMoneyAsync(owner, ct);
        var biz = await BusinessAsync(ct);
        var violations = await ViolationsAsync(ct);
        var yearStart = new DateOnly(today.Year, 1, 1);
        var familyYear = await db.Transactions.Include(t => t.Category)
            .Where(t => t.OwnerId == owner.Id && !t.IsVoided && t.Date >= yearStart && t.Category!.IsFamily)
            .SumAsync(t => t.AmountMinor, ct);
        var open = violations.Where(v => v.IsOpen).ToList();
        var stanbic = await db.Accounts.SingleAsync(a => a.OwnerId == owner.Id && a.Slug == "stanbic", ct);
        var stanbicSnap = await LatestSnapshotAsync(stanbic.Id, ct);
        var stanbicMoney = stanbicSnap is null
            ? MoneyDto.Unknown("Confirm the current Stanbic balance.")
            : MoneyDto.Of(stanbicSnap.AmountMinor, stanbicSnap.Currency, stanbicSnap.Provenance, stanbicSnap.AsOf);
        var facts = new List<LedgerFact>
        {
            Fact("Actually spendable", spendable.Amount),
            Fact("This month income", month.Income),
            Fact("This month personal expenses", month.Expenses),
            Fact("This month savings", month.Savings),
            Fact("This month family support", month.FamilySupport),
            Fact("This month transfers (not expenses)", month.Transfers),
            Fact("This month betting recorded", month.Betting),
            Fact("Remaining spend envelopes", MoneyDto.Of(remainingSpend, Currency.Ngn, Provenance.Confirmed)),
            Fact("OPay monthly allowance", owner.OpayMonthlyAllowanceMinor is long allowance
                ? MoneyDto.Of(allowance, Currency.Ngn, Provenance.Confirmed)
                : MoneyDto.Unknown("OPay monthly allowance")),
            Fact("Housing", housing.Amount),
            new("Housing sentence", housing.Sentence, "plan"),
            Fact("MatchPredictor revenue", biz.Revenue),
            Fact("MatchPredictor expenses", biz.Expenses),
            Fact("MatchPredictor net", biz.Net),
            Fact("Family support this year", MoneyDto.Of(familyYear, Currency.Ngn, Provenance.Confirmed)),
            new("Open rule violations", open.Count == 0 ? "none" : $"{open.Count}: {open[0].Message}", "confirmed"),
            Fact("Stanbic current balance", stanbicMoney)
        };

        if (llm.IsConfigured)
        {
            var user = $"Question: {message}\n\nFacts:\n{LedgerFacts.ToJson(facts)}";
            var text = await llm.CompleteAsync(LedgerFacts.SystemPrompt, user, ct);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return new AssistantResponseDto(message, text, Evidence(facts), false, LedgerFacts.MissingLabels(facts));
            }
        }

        var lower = message.ToLowerInvariant();

        AssistantAnswer answer;
        if (LooksLikeBettingAdvice(lower))
        {
            answer = AssistantEngine.FromFacts(message,
                "UNKNOWN. Finance OS does not give betting advice. I can restate recorded betting spend from the ledger, not what to wager.",
                [], "Betting advice");
        }
        else if (lower.Contains("stanbic") && (lower.Contains("balance") || lower.Contains("how much")))
        {
            answer = stanbicMoney.Minor is null
                ? AssistantEngine.FromFacts("What is my Stanbic balance?",
                    "UNKNOWN. Confirm the current Stanbic balance on Accounts before I can restate it.",
                    [], "Stanbic current balance")
                : AssistantEngine.FromFacts("What is my Stanbic balance?",
                    $"The latest recorded Stanbic figure is {stanbicMoney.Formatted} ({stanbicMoney.ProvenanceLabel}).",
                    []);
        }
        else if (lower.Contains("afford") || lower.Contains("purchase") || lower.Contains("buy"))
        {
            var parsed = QuickEntryParser.Parse(message, today, new Dictionary<string, string>());
            var amount = parsed.AmountMinor ?? 10_000_000;
            answer = AssistantEngine.AffordPurchase(amount, spendable.Amount.Minor ?? 0, remainingSpend, spendable.Amount.Minor is not null);
        }
        else if (lower.Contains("weekend") || lower.Contains("safely spend"))
        {
            var remainingDays = Math.Max(1, end.DayNumber - today.DayNumber);
            answer = AssistantEngine.WeekendSpend(remainingSpend, remainingDays);
        }
        else if (lower.Contains("disappearing") || lower.Contains("what happened"))
        {
            answer = AssistantEngine.FromFacts("What happened to my money this month?",
                $"This month: income {month.Income.Formatted}, personal expenses {month.Expenses.Formatted}, savings {month.Savings.Formatted}, family {month.FamilySupport.Formatted}, career {month.CareerBusiness.Formatted}, betting {month.Betting.Formatted}. Transfers {month.Transfers.Formatted} are not expenses.",
                []);
        }
        else if (lower.Contains("move") && (lower.Contains("month") || lower.Contains("track")))
        {
            answer = AssistantEngine.FromFacts("Am I on track to move?", housing.Sentence,
                housing.Explain.Lines.Select(l => new ExplainLine(l.Label, l.Amount.Minor, Currency.Ngn, Provenance.Confirmed, l.Note)).ToList());
        }
        else if (lower.Contains("opay") && (lower.Contains("transfer") || lower.Contains("should")))
        {
            var opayAllowance = owner.OpayMonthlyAllowanceMinor;
            answer = opayAllowance is null
                ? AssistantEngine.FromFacts("How much should I transfer to OPay?",
                    "UNKNOWN. Choose a single monthly OPay allowance in Settings inside the ₦60,000–₦80,000 band. The app will not invent one.",
                    [], "OPay monthly allowance")
                : AssistantEngine.FromFacts("How much should I transfer to OPay?",
                    $"The chosen OPay allowance is {Money.FromMajor(opayAllowance.Value / 100m, Currency.Ngn)}. Transfer only enough to reach that ceiling after checking this month's OPay spending.",
                    []);
        }
        else if (lower.Contains("family") && (lower.Contains("year") || lower.Contains("given") || lower.Contains("how much")))
        {
            answer = AssistantEngine.FromFacts("How much have I given my family this year?",
                $"Recorded family support this year is {Money.FromMajor(familyYear / 100m, Currency.Ngn)}. Recurring commitments stay separate from one-off help.",
                []);
        }
        else if (lower.Contains("software") || lower.Contains("cursor"))
        {
            answer = AssistantEngine.FromFacts("How much have I spent on software?",
                $"Career/business spending recorded this month is {month.CareerBusiness.Formatted}.",
                []);
        }
        else if (lower.Contains("matchpredictor") || lower.Contains("costing"))
        {
            answer = AssistantEngine.FromFacts("How much has MatchPredictor cost me?",
                $"MatchPredictor revenue {biz.Revenue.Formatted}, expenses {biz.Expenses.Formatted}, net {biz.Net.Formatted}. Personal betting is not included here.",
                biz.Lines.Select(l => new ExplainLine(l.Category, l.Amount.Minor, Currency.Ngn, Provenance.Confirmed, null)).ToList());
        }
        else if (lower.Contains("following") || lower.Contains("on track") || lower.Contains("plan"))
        {
            answer = AssistantEngine.FromFacts("Am I following my financial plan?",
                open.Count == 0
                    ? "No open rule violations are recorded. Continue recording every transaction and reconciling accounts."
                    : $"There are {open.Count} open rule flags. First: {open[0].Message}",
                []);
        }
        else if (lower.Contains("increase") && lower.Contains("invest"))
        {
            answer = AssistantEngine.FromFacts("Can I increase my investment contribution?",
                "Only from money that has no other job. Secondary-income remainder is UNKNOWN until Risevest and Bamboo actual naira charges are entered. Do not take the increase from the emergency fund or housing capital.",
                [], "Actual Risevest naira charge", "Actual Bamboo naira charge");
        }
        else
        {
            answer = AssistantEngine.FromFacts(message,
                "I can answer from your ledger, plan, and confirmed balances. Ask about spendable cash, this month's movement, family, MatchPredictor, housing, OPay, or whether you are following the plan. I will not invent missing balances.",
                []);
        }

        return new AssistantResponseDto(answer.Question, answer.Answer,
            answer.Evidence.Select(MoneyMapping.Line).ToList(), answer.UsedEstimate, answer.MissingFacts);
    }

    private async Task<List<ActionDto>> GenerateActionsAsync(Owner owner, DateOnly today, CancellationToken ct)
    {
        var existing = await db.Actions.Where(a => a.OwnerId == owner.Id && a.ForDate == today).ToListAsync(ct);
        if (existing.Count == 0)
        {
            var generated = new List<FinancialAction>();
            var (start, _) = MoneyMapping.MonthBounds(today);
            if (today.Day >= 27)
            {
                generated.Add(A(owner.Id, today, 1, "Move ₦50,000 to Emergency Fund", "Salary-day checklist. Confirm the real-world transfer yourself."));
                generated.Add(A(owner.Id, today, 2, "Move ₦50,000 to MMF", "Salary-day checklist."));
                generated.Add(A(owner.Id, today, 3, "Transfer ₦100,000 to rotating savings", "Treat as housing capital. Do not spend it."));
            }

            if (today.Day >= 12 && today.Day <= 16)
            {
                generated.Add(A(owner.Id, today, 4, "Apply the ₦400,000 secondary-income waterfall", "Do not move it wholesale to Stanbic. Enter actual Risevest and Bamboo naira charges first."));
            }

            var stanbic = await db.Accounts.SingleAsync(a => a.OwnerId == owner.Id && a.Slug == "stanbic", ct);
            var recon = await ReconcileAsync(stanbic, ct);
            if (recon.Status != ReconciliationStatus.Reconciled)
            {
                generated.Add(A(owner.Id, today, 5, "Reconcile Stanbic", recon.Sentence));
            }

            var opaySpend = await db.Transactions.Include(t => t.Account)
                .Where(t => t.OwnerId == owner.Id && !t.IsVoided && t.Date >= start && t.Account!.Slug == "opay" && t.Type == TransactionType.Expense)
                .SumAsync(t => t.AmountMinor, ct);
            if (owner.OpayMonthlyAllowanceMinor is long allowance && opaySpend > allowance)
            {
                generated.Add(A(owner.Id, today, 6, "Do not transfer additional money to OPay",
                    $"OPay spending is already {Money.FromMajor(opaySpend / 100m, Currency.Ngn)} against a {Money.FromMajor(allowance / 100m, Currency.Ngn)} allowance."));
            }
            else
            {
                generated.Add(A(owner.Id, today, 6, "Review food spending", "Compare food actuals with the ₦45,000 plan before topping up OPay."));
            }

            db.Actions.AddRange(generated);
            await db.SaveChangesAsync(ct);
            existing = generated;
        }

        return existing
            .Where(a => a.State == ActionState.Pending && (a.SnoozeUntil is null || a.SnoozeUntil <= today))
            .OrderBy(a => a.SortOrder)
            .Select(a => new ActionDto(a.Id, a.ForDate, a.Title, a.Detail, a.State.ToString()))
            .ToList();
    }

    private static FinancialAction A(Guid ownerId, DateOnly day, int order, string title, string detail) =>
        new() { OwnerId = ownerId, ForDate = day, SortOrder = order, Title = title, Detail = detail };

    private async Task<Owner> RequireOwner(CancellationToken ct) =>
        await GetOwnerAsync(ct) ?? throw new DomainException("Create the owner first.");

    private async Task AuditAsync(Guid ownerId, string action, string entity, Guid? id, string details, CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog { OwnerId = ownerId, Action = action, Entity = entity, EntityId = id, Details = details });
        await Task.CompletedTask;
    }

    private async Task<BalanceSnapshot?> LatestSnapshotAsync(Guid accountId, CancellationToken ct) =>
        await db.BalanceSnapshots
            .Where(s => s.AccountId == accountId && s.Source != "opening")
            .OrderByDescending(s => s.AsOf)
            .ThenByDescending(s => s.RecordedAtUtc)
            .FirstOrDefaultAsync(ct);

    private async Task<ReconciliationResult> ReconcileAsync(FinancialAccount account, CancellationToken ct)
    {
        var opening = await db.BalanceSnapshots.Where(s => s.AccountId == account.Id && s.Source == "opening")
            .OrderByDescending(s => s.AsOf).FirstOrDefaultAsync(ct);
        var txs = await db.Transactions.Where(t => !t.IsVoided && (t.AccountId == account.Id || t.CounterpartyAccountId == account.Id))
            .ToListAsync(ct);
        long inflows = 0, outflows = 0, fees = 0, adjustments = 0;
        foreach (var tx in txs)
        {
            var directedAtThis = tx.AccountId == account.Id;
            if (tx.Type == TransactionType.Adjustment && directedAtThis) { adjustments += tx.AmountMinor; continue; }
            if (directedAtThis)
            {
                if (tx.Type is TransactionType.Income or TransactionType.Deposit or TransactionType.Refund or TransactionType.BusinessRevenue)
                    inflows += tx.AmountMinor;
                else
                    outflows += tx.AmountMinor;
                fees += tx.FeeMinor;
            }
            else if (tx.IsTransfer && tx.CounterpartyAccountId == account.Id && tx.Type == TransactionType.Transfer)
            {
                inflows += tx.AmountMinor;
            }
        }

        var actual = await LatestSnapshotAsync(account.Id, ct);
        return ReconciliationCalculator.Calculate(account.Id, account.Name, opening?.AmountMinor, inflows, outflows, fees, adjustments,
            actual?.AmountMinor, actual?.AsOf, actual?.Provenance);
    }

    private async Task<(AccountHealth Health, string Sentence)> HealthAsync(
        Owner owner, FinancialAccount account, ReconciliationResult recon, BalanceSnapshot? latest, CancellationToken ct)
    {
        if (latest is null)
        {
            return (AccountHealth.Unknown, $"{account.Name} balance is UNKNOWN. Enter the current figure before a health colour is shown.");
        }

        if (recon.Status == ReconciliationStatus.Unreconciled)
        {
            return (AccountHealth.Attention, recon.Sentence);
        }

        if (account.Slug == "opay" && owner.OpayMonthlyAllowanceMinor is long allowance)
        {
            var (start, end) = MoneyMapping.MonthBounds(MoneyMapping.Today());
            var spent = await db.Transactions.Where(t => t.AccountId == account.Id && !t.IsVoided && t.Date >= start && t.Date <= end && t.Type == TransactionType.Expense)
                .SumAsync(t => t.AmountMinor, ct);
            if (spent > allowance)
            {
                var pct = (spent - allowance) * 100m / allowance;
                return (AccountHealth.Watch, $"OPay spending is {pct:0.#}% above the chosen allowance.");
            }
        }

        if (account.Slug == "access")
        {
            var today = MoneyMapping.Today();
            var due = await db.Subscriptions.Where(s => s.PaymentAccountId == account.Id && s.IsActive && s.NextBillingDate <= today.AddDays(7) && s.AmountMinor > 0)
                .SumAsync(s => s.AmountMinor, ct);
            if (latest.AmountMinor < due)
            {
                return (AccountHealth.Watch, $"Access needs funding for {Money.FromMajor(due / 100m, Currency.Ngn)} due in the next 7 days.");
            }
        }

        if (latest.Provenance != Provenance.Confirmed)
        {
            return (AccountHealth.Unknown, $"{account.Name} still uses a {latest.Provenance} figure from {latest.AsOf:yyyy-MM-dd}. Confirm the current balance.");
        }

        return (AccountHealth.Healthy, $"{account.Name} has a confirmed balance and no open reconciliation difference.");
    }

    private async Task<ExplainDto> BuildNetWorthAsync(Owner owner, CancellationToken ct)
    {
        var holdings = await db.Holdings.Where(h => h.OwnerId == owner.Id).ToListAsync(ct);
        var cashAccounts = await db.Accounts.Where(a => a.OwnerId == owner.Id && a.IncludeInNetWorth &&
            (a.Role == AccountRole.Clearing || a.Role == AccountRole.StrategicBuffer || a.Role == AccountRole.DailySpending || a.Role == AccountRole.CashOnHand || a.Role == AccountRole.BusinessCard))
            .ToListAsync(ct);
        var items = new List<(string, long, Currency, Provenance, bool, bool, bool)>();
        foreach (var account in cashAccounts)
        {
            var snap = await LatestSnapshotAsync(account.Id, ct);
            if (snap is null)
            {
                items.Add((account.Name, 0, account.Currency, Provenance.Unknown, true, false, false));
                continue;
            }

            items.Add((account.Name, snap.AmountMinor, snap.Currency, snap.Provenance, true, false, false));
        }

        foreach (var holding in holdings)
        {
            if (holding.Provenance == Provenance.Unknown)
            {
                continue;
            }

            items.Add((holding.Name, holding.AmountMinor, holding.Currency, holding.Provenance, holding.IncludeInNetWorth, holding.IsExpectedReceivable, false));
        }

        var fxRows = await db.ExchangeRates.Where(r => r.OwnerId == owner.Id && r.From == Currency.Usd && r.To == Currency.Ngn)
            .ToListAsync(ct);
        var fx = fxRows
            .OrderByDescending(r => r.AsOf)
            .ThenByDescending(r => r.Source == "manual")
            .ThenByDescending(r => r.RecordedAtUtc)
            .FirstOrDefault();
        var explanation = NetWorthCalculator.Calculate(items, fx);
        return new ExplainDto(explanation.Metric, explanation.Sentence, explanation.Formula,
            explanation.Lines.Select(MoneyMapping.Line).ToList(),
            explanation.ResultMinor is null
                ? MoneyDto.Unknown("Confirm account and holding balances. Last-known and expected figures are listed in the breakdown.")
                : MoneyDto.Of(explanation.ResultMinor.Value, Currency.Ngn, explanation.ResultProvenance));
    }

    private async Task<(MoneyDto Amount, string Sentence, ExplainDto Explain)> BuildSpendableAsync(Owner owner, CancellationToken ct)
    {
        var assignments = await db.Assignments.Include(a => a.Envelope)
            .Join(db.Accounts, a => a.AccountId, acc => acc.Id, (a, acc) => new { a, acc })
            .Where(x => x.acc.OwnerId == owner.Id)
            .ToListAsync(ct);
        var rows = assignments.Select(x => (
            x.acc.Name, x.acc.Role, x.acc.AllowsDailySpending, x.a.Envelope!.Name, x.a.Envelope.Class, x.a.AmountMinor, x.a.Currency)).ToList();
        var result = SpendableCalculator.FromAssignments(rows, Currency.Ngn);
        var amount = MoneyDto.Of(result.SpendableMinor, Currency.Ngn, Provenance.Confirmed);
        var explain = new ExplainDto("spendable", result.Sentence,
            "Actually spendable = unused spend-class envelopes that sit in a daily-spending account. Clearing, savings, and unassigned money are excluded.",
            result.Lines.Select(MoneyMapping.Line).ToList(), amount);
        return (amount, result.Sentence, explain);
    }

    private async Task<ThisMonthDto> MonthTotalsAsync(Guid ownerId, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var txs = await db.Transactions.Include(t => t.Category).Include(t => t.Envelope)
            .Where(t => t.OwnerId == ownerId && !t.IsVoided && t.Date >= start && t.Date <= end)
            .ToListAsync(ct);
        MoneyDto Sum(Func<LedgerTransaction, bool> pred) =>
            MoneyDto.Of(txs.Where(pred).Sum(t => t.AmountMinor), Currency.Ngn, Provenance.Confirmed);

        return new ThisMonthDto(
            Sum(t => t.Type is TransactionType.Income or TransactionType.BusinessRevenue),
            Sum(t => t.Type == TransactionType.Expense && !t.IsBusiness && t.Category is not { IsBetting: true }),
            Sum(t => t.Type == TransactionType.Savings),
            Sum(t => t.Type == TransactionType.Investment),
            Sum(t => t.Category is { IsFamily: true }),
            Sum(t => t.Envelope is { Slug: "career-business" } || t.Category is { Slug: "software" or "career-business" }),
            Sum(t => t.Category is { IsBetting: true }),
            Sum(t => t.IsTransfer));
    }

    private async Task<MoneyDto> LiquidCashAsync(Owner owner, CancellationToken ct)
    {
        var slugs = new[] { "stanbic", "kuda", "opay", "cash", "access" };
        long confirmed = 0;
        var missing = new List<string>();
        foreach (var slug in slugs)
        {
            var account = await db.Accounts.SingleAsync(a => a.OwnerId == owner.Id && a.Slug == slug, ct);
            var snap = await LatestSnapshotAsync(account.Id, ct);
            if (snap is null)
            {
                missing.Add(account.Name);
                continue;
            }

            if (snap.Provenance == Provenance.Confirmed && snap.Currency == Currency.Ngn)
            {
                confirmed += snap.AmountMinor;
            }
            else
            {
                missing.Add($"{account.Name} ({snap.Provenance} {snap.AsOf:yyyy-MM-dd})");
            }
        }

        return missing.Count == slugs.Length
            ? MoneyDto.Unknown("Confirm Stanbic, Kuda, OPay, cash, and Access balances. Last-known figures are not treated as liquid cash today.")
            : MoneyDto.Of(confirmed, Currency.Ngn, Provenance.Confirmed);
    }

    private async Task<MoneyDto> InvestmentsMoneyAsync(Owner owner, CancellationToken ct)
    {
        var rows = await db.Holdings.Where(h => h.OwnerId == owner.Id && h.IncludeInNetWorth && !h.IsExpectedReceivable && h.Currency == Currency.Ngn).ToListAsync(ct);
        var confirmed = rows.Where(h => h.Provenance == Provenance.Confirmed).Sum(h => h.AmountMinor);
        return confirmed == 0
            ? MoneyDto.Unknown("No confirmed investment balances yet. Last-known holdings are on the Investments screen.")
            : MoneyDto.Of(confirmed, Currency.Ngn, Provenance.Confirmed);
    }

    private async Task<MoneyDto> HoldingMoneyAsync(Guid ownerId, string slug, CancellationToken ct)
    {
        var holding = await db.Holdings.SingleOrDefaultAsync(h => h.OwnerId == ownerId && h.Slug == slug, ct);
        if (holding is null) return MoneyDto.Unknown($"No {slug} holding has been recorded.");
        return holding.Provenance == Provenance.Unknown
            ? MoneyDto.Unknown(holding.StatusNote, holding.Currency)
            : MoneyDto.Of(holding.AmountMinor, holding.Currency, holding.Provenance, holding.AsOf);
    }

    private async Task<(MoneyDto Amount, string Sentence, ExplainDto Explain)> HousingMoneyAsync(Owner owner, CancellationToken ct)
    {
        var piggy = await db.Accounts.SingleAsync(a => a.OwnerId == owner.Id && a.Slug == "piggyvest-housing", ct);
        var piggySnap = await LatestSnapshotAsync(piggy.Id, ct);
        long? confirmedPiggy = piggySnap is { Provenance: Provenance.Confirmed } ? piggySnap.AmountMinor : null;
        var home = await db.Holdings.SingleAsync(h => h.OwnerId == owner.Id && h.Slug == "home", ct);
        long? homeMinor = home.Provenance == Provenance.Unknown ? null : home.AmountMinor;
        var rotating = await db.Holdings.SingleOrDefaultAsync(h => h.OwnerId == owner.Id && h.Slug == "rotating", ct);
        long? rotatingMinor = rotating is null || rotating.Provenance == Provenance.Unknown ? null : rotating.AmountMinor;
        var projection = HousingProjection.Project(
            confirmedPiggy, homeMinor, 5, 25_000_000, 500_000, rotatingMinor, 300_000_000, MoneyMapping.Today());
        var amount = confirmedPiggy is null
            ? MoneyDto.Unknown("Enter the current PiggyVest housing balance.")
            : MoneyDto.Of(confirmedPiggy.Value, Currency.Ngn, Provenance.Confirmed);
        var explain = new ExplainDto("housing-gap", projection.Sentence,
            "Confirmed housing vault + entered home savings + future planned housing contributions + expected rotating payout. Monthly rotating ₦100k is the same pool as the expected payout and is not added twice.",
            projection.Lines.Select(MoneyMapping.Line).ToList(), amount);
        return (amount, projection.Sentence, explain);
    }

    private async Task<ExplainDto> SavingsRateExplainAsync(Owner owner, CancellationToken ct)
    {
        var (start, end) = MoneyMapping.MonthBounds(MoneyMapping.Today());
        var month = await MonthTotalsAsync(owner.Id, start, end, ct);
        var income = month.Income.Minor ?? 0;
        var saved = (month.Savings.Minor ?? 0) + (month.Investments.Minor ?? 0);
        if (income == 0)
        {
            return new ExplainDto("savings-rate",
                "Savings rate is UNKNOWN this month because no income has been recorded.",
                "Savings + investments ÷ income, using recorded transactions only.",
                [], MoneyDto.Unknown("Record income first."));
        }

        return new ExplainDto("savings-rate",
            $"You directed {Money.FromMajor(saved / 100m, Currency.Ngn)} of {Money.FromMajor(income / 100m, Currency.Ngn)} toward savings and investments this month.",
            "Savings + investments ÷ recorded income. Transfers are not income.",
            [
                new ExplainLineDto("Income", month.Income, null),
                new ExplainLineDto("Savings", month.Savings, null),
                new ExplainLineDto("Investments", month.Investments, null)
            ],
            MoneyDto.Of(saved, Currency.Ngn, Provenance.Confirmed));
    }

    private async Task<ExplainDto> BudgetExplainAsync(Owner owner, string categorySlug, CancellationToken ct)
    {
        var items = await BudgetAsync(ct);
        var item = items.FirstOrDefault(i => i.Category.Replace(" ", "-").Replace("/", "-").Equals(categorySlug, StringComparison.OrdinalIgnoreCase))
                   ?? items.FirstOrDefault(i => i.Category.Equals(categorySlug, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            throw new DomainException($"No budget line named '{categorySlug}'.");
        }

        return new ExplainDto("budget-" + categorySlug, item.Sentence, "Budget − actual = remaining. Status bands are the ones you configured.",
            [
                new ExplainLineDto("Budget", item.Budget, null),
                new ExplainLineDto("Actual", item.Actual, null),
                new ExplainLineDto("Remaining", item.Remaining, null)
            ],
            item.Remaining);
    }

    private async Task<MoneyDto> GoalCurrentAsync(Owner owner, Goal goal, CancellationToken ct)
    {
        if (goal.Kind == GoalKind.Retirement)
        {
            return MoneyDto.Unknown("Retirement progress is not a single balance. Use the Investments calculator after entering assumptions.");
        }

        if (goal.FundingAccountId is Guid accountId)
        {
            var snap = await LatestSnapshotAsync(accountId, ct);
            if (snap is null) return MoneyDto.Unknown($"Enter the current balance for {goal.Name}.");
            return MoneyDto.Of(snap.AmountMinor, snap.Currency, snap.Provenance, snap.AsOf);
        }

        var holding = await db.Holdings.SingleOrDefaultAsync(h => h.OwnerId == owner.Id && h.Slug == goal.Slug, ct);
        return holding is null
            ? MoneyDto.Unknown($"No balance has been linked to {goal.Name}.")
            : MoneyDto.Of(holding.AmountMinor, holding.Currency, holding.Provenance, holding.AsOf);
    }

    private async Task<List<string>> UnknownFactsAsync(Owner owner, CancellationToken ct)
    {
        var needed = new List<string>();
        foreach (var slug in new[] { "stanbic", "piggyvest-housing", "access" })
        {
            var account = await db.Accounts.SingleAsync(a => a.OwnerId == owner.Id && a.Slug == slug, ct);
            var snap = await LatestSnapshotAsync(account.Id, ct);
            if (snap is null || snap.Provenance != Provenance.Confirmed)
            {
                needed.Add($"Enter a confirmed {account.Name} balance.");
            }
        }

        if (!await db.ExchangeRates.AnyAsync(r => r.OwnerId == owner.Id, ct))
        {
            needed.Add("USD holdings stay in dollars until you enter an FX rate.");
        }

        var pension = await db.Pensions.SingleAsync(p => p.OwnerId == owner.Id, ct);
        if (pension.CurrentBalanceMinor is null)
        {
            needed.Add("RSA / pension balance is UNKNOWN.");
        }

        return needed;
    }

    private static TransactionDto ToDto(LedgerTransaction tx) =>
        new(tx.Id, tx.Date, tx.Type.ToString(), tx.AccountId, tx.Account?.Name ?? "",
            tx.CounterpartyAccountId,
            MoneyDto.Of(tx.AmountMinor, tx.Currency, Provenance.Confirmed, tx.Date),
            MoneyDto.Of(tx.FeeMinor, tx.Currency, Provenance.Confirmed, tx.Date),
            tx.Category?.Name ?? "", tx.Description, tx.Merchant, tx.IsBusiness, tx.IsTransfer, tx.IsVoided,
            tx.Category?.IsBetting ?? false);

    private static string Csv(string? value) =>
        "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";

    private static string[] SplitCsv(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        foreach (var ch in line)
        {
            if (ch == '"') { quoted = !quoted; continue; }
            if (ch == ',' && !quoted) { result.Add(current.ToString()); current.Clear(); continue; }
            current.Append(ch);
        }

        result.Add(current.ToString());
        return result.ToArray();
    }

    private static string SnapshotSource(string? source)
    {
        var value = string.IsNullOrWhiteSpace(source) ? "manual" : source.Trim().ToLowerInvariant();
        if (value is not ("opening" or "manual"))
        {
            throw new DomainException("Enter an opening balance or the current figure. Other sources are not recorded.");
        }

        return value;
    }

    private static bool LooksLikeBettingAdvice(string lower) =>
        (lower.Contains("bet") || lower.Contains("wager") || lower.Contains("odds"))
        && (lower.Contains("should") || lower.Contains("advice") || lower.Contains("tip") || lower.Contains("pick"));

    private static LedgerFact Fact(string label, MoneyDto money) =>
        new(label, money.Formatted, money.ProvenanceLabel, money.Minor);

    private static List<ExplainLineDto> Evidence(IReadOnlyList<LedgerFact> facts) =>
        facts
            .Where(f => f.Minor is not null)
            .Select(f => new ExplainLineDto(f.Label, MoneyDto.Of(f.Minor!.Value, Currency.Ngn, Provenance.Confirmed), f.Provenance))
            .ToList();

    private static byte[] MinimalXlsx(string sheetName, string csv)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteZip(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            WriteZip(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            WriteZip(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);
            WriteZip(zip, "xl/workbook.xml", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="{sheetName}" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            var rows = new StringBuilder();
            var r = 1;
            foreach (var line in csv.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                rows.Append($"<row r=\"{r}\">");
                var c = 0;
                foreach (var cell in SplitCsv(line.Trim()))
                {
                    var col = (char)('A' + c);
                    rows.Append($"<c r=\"{col}{r}\" t=\"inlineStr\"><is><t>{System.Security.SecurityElement.Escape(cell)}</t></is></c>");
                    c++;
                }

                rows.Append("</row>");
                r++;
            }

            WriteZip(zip, "xl/worksheets/sheet1.xml", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>{rows}</sheetData></worksheet>
                """);
        }

        return stream.ToArray();
    }

    private static void WriteZip(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(content);
    }
}
