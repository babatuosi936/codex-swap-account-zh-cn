using System.Globalization;

namespace CodexProfileOverlay.Core.Services;

public static class UsageDisplayFormatter
{
    public static string FormatLocal(
        DateTimeOffset value,
        CultureInfo? culture = null,
        TimeZoneInfo? timeZone = null)
    {
        DateTimeOffset local = TimeZoneInfo.ConvertTime(value, timeZone ?? TimeZoneInfo.Local);
        return local.ToString("g", culture ?? CultureInfo.CurrentCulture);
    }
}
