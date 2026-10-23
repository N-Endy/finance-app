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
        var result = await TryCallModelAsync(_model, system, user, false, ct);
        if (result is null && _model == DefaultModel)
        {
            // Fallback to gpt-4o-mini if gpt-6-luna is unavailable on user API key tier
            result = await TryCallModelAsync("gpt-4o-mini", system, user, false, ct);
        }
        return result;
    }

    public async Task<string?> CompleteJsonAsync(string system, string user, CancellationToken ct)
    {
        if (!IsConfigured) return null;
        var result = await TryCallModelAsync(_model, system, user, true, ct);
        if (result is null && _model == DefaultModel)
        {
            result = await TryCallModelAsync("gpt-4o-mini", system, user, true, ct);
        }
        return result;
    }

    private async Task<string?> TryCallModelAsync(string model, string system, string user, bool jsonMode, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            
            object payload;
            if (model.StartsWith("gpt-6"))
            {
                payload = jsonMode
                    ? new
                    {
                        model,
                        reasoning_effort = "none",
                        response_format = new { type = "json_object" },
                        messages = new object[]
                        {
                            new { role = "system", content = system },
                            new { role = "user", content = user }
                        }
                    }
                    : new
                    {
                        model,
                        reasoning_effort = "none",
                        messages = new object[]
                        {
                            new { role = "system", content = system },
                            new { role = "user", content = user }
                        }
                    };
            }
            else
            {
                payload = jsonMode
                    ? new
                    {
                        model,
                        response_format = new { type = "json_object" },
                        messages = new object[]
                        {
                            new { role = "system", content = system },
                            new { role = "user", content = user }
                        }
                    }
                    : new
                    {
                        model,
                        messages = new object[]
                        {
                            new { role = "system", content = system },
                            new { role = "user", content = user }
                        }
                    };
            }

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
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
