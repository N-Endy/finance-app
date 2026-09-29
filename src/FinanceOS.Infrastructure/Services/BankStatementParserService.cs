using System.Globalization;
using System.Text.RegularExpressions;
using FinanceOS.Application.Contracts;
using FinanceOS.Domain;
using FinanceOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using FinanceOS.Infrastructure.Data;

namespace FinanceOS.Infrastructure.Services;

public interface IBankStatementParserService
{
    Task<StatementParseResultDto> ParseStatementAsync(
        Guid ownerId,
        StatementParseRequest request,
        CancellationToken ct);
}

public sealed partial class BankStatementParserService(FinanceDbContext db) : IBankStatementParserService
{
    public async Task<StatementParseResultDto> ParseStatementAsync(
        Guid ownerId,
        StatementParseRequest request,
        CancellationToken ct)
    {
        var accounts = await db.Accounts.Where(a => a.OwnerId == ownerId).ToListAsync(ct);
        var categories = await db.Categories.Where(c => c.OwnerId == ownerId).ToListAsync(ct);

        var existingTxs = await db.Transactions
            .Where(t => t.OwnerId == ownerId && !t.IsVoided)
            .OrderByDescending(t => t.Date)
            .Take(1500)
            .Select(t => new { t.Date, t.AmountMinor, t.Description, t.AccountId })
            .ToListAsync(ct);

        var content = request.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new DomainException("Statement content cannot be empty.");
        }

        var lines = content.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            throw new DomainException("No rows found in the provided statement.");
        }

        // Determine format
        var format = DetectFormat(request.BankFormat, lines[0]);
        var headerIndex = 0;
        while (headerIndex < lines.Length && !IsHeaderLine(lines[headerIndex]))
        {
            headerIndex++;
        }

        if (headerIndex >= lines.Length)
        {
            headerIndex = 0; // fallback to first line
        }

        var headerCols = SplitCsvLine(lines[headerIndex]);
        var colMap = MapColumns(headerCols, format);

        var defaultAccount = request.DefaultAccountId.HasValue
            ? accounts.FirstOrDefault(a => a.Id == request.DefaultAccountId.Value)
            : DetectAccountFromFormat(format, accounts);

        var parsedItems = new List<ParsedTransactionDraftDto>();
        var duplicatesCount = 0;
        var transfersCount = 0;

        for (int i = headerIndex + 1; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var cols = SplitCsvLine(rawLine);
            if (cols.Length < 2) continue;

            var draft = ParseRow(cols, colMap, format, defaultAccount, accounts, categories, i - headerIndex);
            if (draft is null) continue;

            // Check duplicate
            var draftMinor = (long)Math.Round(draft.Amount * 100m, MidpointRounding.AwayFromZero);
            var isDuplicate = existingTxs.Any(e =>
                e.AccountId == draft.AccountId &&
                Math.Abs(e.Date.DayNumber - draft.Date.DayNumber) <= 1 &&
                Math.Abs(e.AmountMinor - draftMinor) <= 1);

            string? duplicateReason = null;
            if (isDuplicate)
            {
                duplicatesCount++;
                duplicateReason = $"Potential duplicate: an existing record of {draft.Amount:N2} was recorded near {draft.Date:yyyy-MM-dd}.";
            }

            if (draft.IsTransfer)
            {
                transfersCount++;
            }

            parsedItems.Add(draft with
            {
                IsDuplicate = isDuplicate,
                DuplicateReason = duplicateReason
            });
        }

        return new StatementParseResultDto(
            format,
            parsedItems.Count,
            duplicatesCount,
            transfersCount,
            parsedItems);
    }

    private static string DetectFormat(string? requestedFormat, string headerLine)
    {
        if (!string.IsNullOrWhiteSpace(requestedFormat) && requestedFormat.ToLowerInvariant() switch
        {
            "opay" => true,
            "stanbic" => true,
            "kuda" => true,
            "access" => true,
            _ => false
        })
        {
            return requestedFormat.ToLowerInvariant();
        }

        var lower = headerLine.ToLowerInvariant();
        if (lower.Contains("order no") || lower.Contains("reference") && lower.Contains("opay") || lower.Contains("transaction time"))
            return "opay";
        if (lower.Contains("value date") && (lower.Contains("stanbic") || lower.Contains("credit") && lower.Contains("debit")))
            return "stanbic";
        if (lower.Contains("money in") || lower.Contains("money out") || lower.Contains("kuda"))
            return "kuda";
        if (lower.Contains("trans date") || lower.Contains("access bank"))
            return "access";

        return "generic";
    }

    private static FinancialAccount? DetectAccountFromFormat(string format, List<FinancialAccount> accounts)
    {
        return format switch
        {
            "opay" => accounts.FirstOrDefault(a => a.Slug.Contains("opay", StringComparison.OrdinalIgnoreCase) || a.Name.Contains("OPay", StringComparison.OrdinalIgnoreCase)),
            "stanbic" => accounts.FirstOrDefault(a => a.Slug.Contains("stanbic", StringComparison.OrdinalIgnoreCase) || a.Name.Contains("Stanbic", StringComparison.OrdinalIgnoreCase)),
            "kuda" => accounts.FirstOrDefault(a => a.Slug.Contains("kuda", StringComparison.OrdinalIgnoreCase) || a.Name.Contains("Kuda", StringComparison.OrdinalIgnoreCase)),
            "access" => accounts.FirstOrDefault(a => a.Slug.Contains("access", StringComparison.OrdinalIgnoreCase) || a.Name.Contains("Access", StringComparison.OrdinalIgnoreCase)),
            _ => accounts.FirstOrDefault()
        };
    }

    private static bool IsHeaderLine(string line)
    {
        var lower = line.ToLowerInvariant();
        return lower.Contains("date") || lower.Contains("time") || lower.Contains("amount") || lower.Contains("debit") || lower.Contains("narration") || lower.Contains("description");
    }

    private sealed record ColumnMap(
        int DateIndex,
        int DescriptionIndex,
        int? AmountIndex,
        int? DebitIndex,
        int? CreditIndex,
        int? TypeIndex,
        int? ReferenceIndex);

    private static ColumnMap MapColumns(string[] headers, string format)
    {
        int dateIdx = -1, descIdx = -1, amtIdx = -1, debitIdx = -1, creditIdx = -1, typeIdx = -1, refIdx = -1;

        for (int i = 0; i < headers.Length; i++)
        {
            var h = headers[i].Trim().ToLowerInvariant();
            if (dateIdx == -1 && (h.Contains("date") || h.Contains("time"))) dateIdx = i;
            else if (descIdx == -1 && (h.Contains("desc") || h.Contains("narration") || h.Contains("remark") || h.Contains("detail") || h.Contains("particular"))) descIdx = i;
            else if (debitIdx == -1 && (h == "debit" || h.Contains("money out") || h.Contains("paid out") || h.Contains("withdrawal"))) debitIdx = i;
            else if (creditIdx == -1 && (h == "credit" || h.Contains("money in") || h.Contains("paid in") || h.Contains("deposit"))) creditIdx = i;
            else if (amtIdx == -1 && h.Contains("amount")) amtIdx = i;
            else if (typeIdx == -1 && (h == "type" || h.Contains("trans type") || h.Contains("dr/cr"))) typeIdx = i;
            else if (refIdx == -1 && (h.Contains("ref") || h.Contains("order"))) refIdx = i;
        }

        // Fallbacks
        if (dateIdx == -1) dateIdx = 0;
        if (descIdx == -1) descIdx = headers.Length > 1 ? 1 : 0;
        if (amtIdx == -1 && debitIdx == -1 && creditIdx == -1 && headers.Length > 2) amtIdx = 2;

        return new ColumnMap(dateIdx, descIdx, amtIdx >= 0 ? amtIdx : null, debitIdx >= 0 ? debitIdx : null, creditIdx >= 0 ? creditIdx : null, typeIdx >= 0 ? typeIdx : null, refIdx >= 0 ? refIdx : null);
    }

    private static ParsedTransactionDraftDto? ParseRow(
        string[] cols,
        ColumnMap map,
        string format,
        FinancialAccount? defaultAccount,
        List<FinancialAccount> accounts,
        List<Category> categories,
        int index)
    {
        if (cols.Length <= map.DateIndex) return null;

        var rawDate = cols[map.DateIndex].Trim();
        if (!TryParseDate(rawDate, out var date))
        {
            return null;
        }

        var rawDesc = map.DescriptionIndex < cols.Length ? cols[map.DescriptionIndex].Trim() : string.Empty;
        var rawType = map.TypeIndex.HasValue && map.TypeIndex.Value < cols.Length ? cols[map.TypeIndex.Value].Trim() : string.Empty;

        decimal amount = 0;
        var isIncome = false;

        if (map.DebitIndex.HasValue && map.CreditIndex.HasValue)
        {
            var rawDebit = map.DebitIndex.Value < cols.Length ? CleanNumber(cols[map.DebitIndex.Value]) : "0";
            var rawCredit = map.CreditIndex.Value < cols.Length ? CleanNumber(cols[map.CreditIndex.Value]) : "0";

            decimal.TryParse(rawDebit, CultureInfo.InvariantCulture, out var debit);
            decimal.TryParse(rawCredit, CultureInfo.InvariantCulture, out var credit);

            if (credit > 0)
            {
                amount = credit;
                isIncome = true;
            }
            else if (debit > 0)
            {
                amount = debit;
                isIncome = false;
            }
            else
            {
                return null;
            }
        }
        else if (map.AmountIndex.HasValue && map.AmountIndex.Value < cols.Length)
        {
            var cleanAmt = CleanNumber(cols[map.AmountIndex.Value]);
            if (!decimal.TryParse(cleanAmt, CultureInfo.InvariantCulture, out amount))
            {
                return null;
            }

            if (amount < 0)
            {
                amount = Math.Abs(amount);
                isIncome = false;
            }
            else if (rawType.Contains("credit", StringComparison.OrdinalIgnoreCase) ||
                     rawType.Contains("in", StringComparison.OrdinalIgnoreCase) ||
                     rawType.Contains("received", StringComparison.OrdinalIgnoreCase) ||
                     cols[map.AmountIndex.Value].Contains('+'))
            {
                isIncome = true;
            }
            else if (rawType.Contains("debit", StringComparison.OrdinalIgnoreCase) ||
                     rawType.Contains("out", StringComparison.OrdinalIgnoreCase) ||
                     rawType.Contains("paid", StringComparison.OrdinalIgnoreCase) ||
                     cols[map.AmountIndex.Value].Contains('-'))
            {
                isIncome = false;
            }
        }
        else
        {
            return null;
        }

        if (amount <= 0) return null;

        // Auto-detect inter-account transfer
        var (isTransfer, counterparty) = DetectTransfer(rawDesc, defaultAccount, accounts);

        // Auto-detect category
        var (category, needsReview) = isTransfer
            ? (null, false)
            : DetectCategory(rawDesc, isIncome, categories);

        var transactionType = isTransfer ? "Transfer" : (isIncome ? "Income" : "Expense");

        return new ParsedTransactionDraftDto(
            index,
            date,
            SanitizeDescription(rawDesc),
            amount,
            "NGN",
            transactionType,
            defaultAccount?.Id,
            defaultAccount?.Name,
            counterparty?.Id,
            counterparty?.Name,
            category?.Id,
            category?.Name,
            isTransfer,
            false,
            null,
            needsReview,
            rawDesc);
    }

    private static (bool IsTransfer, FinancialAccount? Counterparty) DetectTransfer(
        string narration,
        FinancialAccount? sourceAccount,
        List<FinancialAccount> accounts)
    {
        var upper = narration.ToUpperInvariant();
        var transferKeywords = new[] { "TRF TO", "TRF FROM", "TRANSFER TO", "TRANSFER FROM", "INTER-BANK", "NIP/" };
        var hasTransferKeyword = transferKeywords.Any(k => upper.Contains(k));

        foreach (var acc in accounts)
        {
            if (sourceAccount != null && acc.Id == sourceAccount.Id) continue;

            var matchSlug = upper.Contains(acc.Slug.ToUpperInvariant());
            var matchName = upper.Contains(acc.Name.ToUpperInvariant());
            var matchBank = upper.Contains(acc.Institution.ToUpperInvariant());

            if ((hasTransferKeyword && (matchSlug || matchName || matchBank)) ||
                (upper.Contains("STANBIC") && acc.Institution.Contains("Stanbic", StringComparison.OrdinalIgnoreCase)) ||
                (upper.Contains("OPAY") && acc.Institution.Contains("OPay", StringComparison.OrdinalIgnoreCase)) ||
                (upper.Contains("KUDA") && acc.Institution.Contains("Kuda", StringComparison.OrdinalIgnoreCase)) ||
                (upper.Contains("ACCESS") && acc.Institution.Contains("Access", StringComparison.OrdinalIgnoreCase)))
            {
                return (true, acc);
            }
        }

        if (hasTransferKeyword && (upper.Contains("SELF") || upper.Contains("OWN ACCOUNT")))
        {
            return (true, null);
        }

        return (false, null);
    }

    private static (Category? Category, bool NeedsReview) DetectCategory(
        string narration,
        bool isIncome,
        List<Category> categories)
    {
        var upper = narration.ToUpperInvariant();

        if (isIncome)
        {
            var incomeCat = categories.FirstOrDefault(c => c.Name.Contains("Salary", StringComparison.OrdinalIgnoreCase) || c.Slug.Contains("salary", StringComparison.OrdinalIgnoreCase));
            return (incomeCat, incomeCat is null);
        }

        // Betting
        if (upper.Contains("SPORTY") || upper.Contains("BET9JA") || upper.Contains("1XBET") || upper.Contains("BETWAY"))
        {
            var betCat = categories.FirstOrDefault(c => c.IsBetting || c.Slug.Contains("bet", StringComparison.OrdinalIgnoreCase));
            return (betCat, false);
        }

        // Airtime / Utilities
        if (upper.Contains("AIRTIME") || upper.Contains("MTN") || upper.Contains("AIRTEL") || upper.Contains("GLO") || upper.Contains("VTU") || upper.Contains("DATA"))
        {
            var cat = categories.FirstOrDefault(c => c.Slug.Contains("utility", StringComparison.OrdinalIgnoreCase) || c.Name.Contains("Utilities", StringComparison.OrdinalIgnoreCase) || c.Name.Contains("Airtime", StringComparison.OrdinalIgnoreCase));
            return (cat, cat is null);
        }

        // Transport
        if (upper.Contains("UBER") || upper.Contains("BOLT") || upper.Contains("FUEL") || upper.Contains("PETROL") || upper.Contains("TRANSPORT"))
        {
            var cat = categories.FirstOrDefault(c => c.Slug.Contains("transport", StringComparison.OrdinalIgnoreCase) || c.Name.Contains("Transport", StringComparison.OrdinalIgnoreCase));
            return (cat, cat is null);
        }

        // Food & Dining
        if (upper.Contains("KFC") || upper.Contains("DOMINO") || upper.Contains("FOOD") || upper.Contains("REPUBLIC") || upper.Contains("CHICKEN") || upper.Contains("SUPERMARKET") || upper.Contains("EATERY"))
        {
            var cat = categories.FirstOrDefault(c => c.Slug.Contains("food", StringComparison.OrdinalIgnoreCase) || c.Name.Contains("Food", StringComparison.OrdinalIgnoreCase) || c.Name.Contains("Groceries", StringComparison.OrdinalIgnoreCase));
            return (cat, cat is null);
        }

        // Family Support
        if (upper.Contains("MOM") || upper.Contains("DAD") || upper.Contains("FAMILY") || upper.Contains("PARENTS"))
        {
            var cat = categories.FirstOrDefault(c => c.IsFamily || c.Slug.Contains("family", StringComparison.OrdinalIgnoreCase));
            return (cat, false);
        }

        // Subscriptions
        if (upper.Contains("NETFLIX") || upper.Contains("APPLE.COM") || upper.Contains("SPOTIFY") || upper.Contains("GOOGLE") || upper.Contains("YOUTUBE"))
        {
            var cat = categories.FirstOrDefault(c => c.Slug.Contains("subscription", StringComparison.OrdinalIgnoreCase) || c.Name.Contains("Subscription", StringComparison.OrdinalIgnoreCase));
            return (cat, cat is null);
        }

        return (null, true);
    }

    private static string SanitizeDescription(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Transaction";
        var cleaned = Regex.Replace(raw, @"\s+", " ").Trim();
        return cleaned.Length > 120 ? cleaned[..120] : cleaned;
    }

    private static string CleanNumber(string val)
    {
        return Regex.Replace(val, @"[^\d.-]", "");
    }

    private static bool TryParseDate(string raw, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var cleaned = raw.Split(' ')[0]; // remove time if present
        string[] formats = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "MM/dd/yyyy", "yyyy/MM/dd", "d-MMM-yyyy", "dd-MMM-yyyy"];

        if (DateOnly.TryParseExact(cleaned, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        return false;
    }

    private static string[] SplitCsvLine(string line)
    {
        var list = new List<string>();
        var inQuotes = false;
        var cur = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                list.Add(cur.ToString().Trim());
                cur.Clear();
            }
            else
            {
                cur.Append(c);
            }
        }
        list.Add(cur.ToString().Trim());
        return [.. list];
    }
}
