using System.Globalization;
using System.Text.Json;

namespace FinanceOS.Infrastructure.Services;

public sealed class FxRateClient
{
    public const string SourceName = "open.er-api.com";

    private readonly HttpClient _http;

    public FxRateClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<(decimal Rate, DateOnly? AsOf)?> FetchUsdNgnAsync(CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync("v6/latest/USD", ct);
            if (!response.IsSuccessStatusCode) return null;
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(raw)) return null;
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (!root.TryGetProperty("rates", out var rates) || !rates.TryGetProperty("NGN", out var ngn))
                return null;
            if (!TryReadDecimal(ngn, out var rate) || rate <= 0)
                return null;
            DateOnly? asOf = null;
            if (root.TryGetProperty("time_last_update_utc", out var updated) &&
                DateTime.TryParse(updated.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var when))
            {
                asOf = DateOnly.FromDateTime(when);
            }

            return (rate, asOf);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryReadDecimal(JsonElement element, out decimal rate)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out rate))
            return true;
        return decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out rate);
    }
}
