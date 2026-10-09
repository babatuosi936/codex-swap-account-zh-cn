using System.Text;
using System.Text.Json;

namespace CodexProfileOverlay.Core.Services;

/// <summary>Compare account identity, never refreshed tokens or display names.</summary>
public static class InstanceAccountIdentity
{
    public static string? Read(string authFile)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(authFile));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("tokens", out JsonElement tokens)
                || tokens.ValueKind != JsonValueKind.Object) return null;
            if (tokens.TryGetProperty("account_id", out JsonElement account)
                && account.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(account.GetString())) return account.GetString();
            if (!tokens.TryGetProperty("id_token", out JsonElement token)
                || token.ValueKind != JsonValueKind.String) return null;
            string[] parts = (token.GetString() ?? "").Split('.');
            if (parts.Length != 3) return null;
            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            using JsonDocument claims = JsonDocument.Parse(Encoding.UTF8.GetString(
                Convert.FromBase64String(payload.PadRight((payload.Length + 3) / 4 * 4, '='))));
            if (claims.RootElement.ValueKind == JsonValueKind.Object
                && claims.RootElement.TryGetProperty("https://api.openai.com/auth", out JsonElement auth)
                && auth.ValueKind == JsonValueKind.Object
                && auth.TryGetProperty("chatgpt_account_id", out account)
                && account.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(account.GetString())) return account.GetString();
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or JsonException or FormatException or ArgumentException) { return null; }
    }
}
