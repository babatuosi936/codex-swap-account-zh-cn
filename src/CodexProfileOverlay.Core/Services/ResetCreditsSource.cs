using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using CodexProfileOverlay.Core.Models;

namespace CodexProfileOverlay.Core.Services;

// Read-only supplement, matching Codex Accounts Manager's reset-credit query.
// No redemption, auth changes, request/response bodies or credentials in logs.
public sealed class ResetCreditsSource
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(5) };

    public async Task PopulateAsync(string profileDirectory, UsageSnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            using var auth = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(profileDirectory, "auth.json"), cancellationToken).ConfigureAwait(false));
            if (auth.RootElement.ValueKind != JsonValueKind.Object || !auth.RootElement.TryGetProperty("tokens", out var tokens)
                || tokens.ValueKind != JsonValueKind.Object || !tokens.TryGetProperty("access_token", out var access)
                || access.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(access.GetString())) return;
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://chatgpt.com/backend-api/wham/rate-limit-reset-credits");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.GetString());
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            string? account = InstanceAccountIdentity.Read(Path.Combine(profileDirectory, "auth.json"));
            if (account is not null) request.Headers.TryAddWithoutValidation("ChatGPT-Account-Id", account);
            using var response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 1024 * 1024) return;
            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (json.Length <= 1024 * 1024) Apply(json, snapshot, DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or HttpRequestException or InvalidOperationException) { }
    }

    public static void Apply(string json, UsageSnapshot snapshot, DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(json);
        var value = document.RootElement;
        if (value.ValueKind != JsonValueKind.Object) return;
        if (value.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object) value = data;
        int? count = null;
        foreach (string name in new[] { "available_count", "availableCount" })
            if (value.TryGetProperty(name, out var number) && int.TryParse(number.ToString(), out int parsed) && parsed >= 0) { count = parsed; break; }
        var expires = Timestamp(value, "next_expires_at", "nextExpiresAt", "reset_credits_next_expires_at", "resetCreditsNextExpiresAt");
        if (value.TryGetProperty("credits", out var credits) && credits.ValueKind == JsonValueKind.Array)
        {
            int available = 0;
            DateTimeOffset? nearest = null;
            foreach (var credit in credits.EnumerateArray())
            {
                if (credit.ValueKind != JsonValueKind.Object) continue;
                string? status = credit.TryGetProperty("status", out var statusElement) ? statusElement.ToString().ToLowerInvariant()
                    : credit.TryGetProperty("state", out var state) ? state.ToString().ToLowerInvariant() : null;
                if (status is "redeemed" or "used" or "consumed" or "expired") continue;
                DateTimeOffset? at = Timestamp(credit, "expires_at", "expire_at", "expiresAt");
                if (at <= now) continue;
                available++;
                if (at is not null && (nearest is null || at < nearest)) nearest = at;
            }
            count ??= available;
            expires ??= nearest;
        }
        snapshot.ResetCreditsRemaining = count;
        snapshot.ResetCreditsExpiresAt = count > 0 ? expires : null;
    }

    private static DateTimeOffset? Timestamp(JsonElement value, params string[] names)
    {
        foreach (string name in names)
        {
            if (!value.TryGetProperty(name, out var item)) continue;
            string text = item.ToString();
            if (long.TryParse(text, out long number) && number > 0)
            {
                try { return number > 1_000_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds(number) : DateTimeOffset.FromUnixTimeSeconds(number); }
                catch (ArgumentOutOfRangeException) { continue; }
            }
            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)) return at;
        }
        return null;
    }
}
