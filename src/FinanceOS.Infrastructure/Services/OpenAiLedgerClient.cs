using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FinanceOS.Infrastructure.Services;

public sealed class OpenAiLedgerClient
{
    public const string DefaultModel = "gpt-6-luna";

    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly string _model;

    public OpenAiLedgerClient(HttpClient http)
        : this(http, Environment.GetEnvironmentVariable("OPENAI_API_KEY"), Environment.GetEnvironmentVariable("OPENAI_MODEL"))
    {
    }

    public static OpenAiLedgerClient Create(HttpClient http, string? apiKey, string? model) =>
        new(http, apiKey, model);

    private OpenAiLedgerClient(HttpClient http, string? apiKey, string? model)
    {
        _http = http;
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();
    }

    public bool IsConfigured => _apiKey is not null;

    public async Task<string?> CompleteAsync(string system, string user, CancellationToken ct)
    {
        if (!IsConfigured) return null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            var payload = new
            {
                model = _model,
                reasoning_effort = "none",
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user }
                }
            };
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(raw)) return null;
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return null;
            var content = choices[0].GetProperty("message").GetProperty("content").GetString();
            return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
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
}
