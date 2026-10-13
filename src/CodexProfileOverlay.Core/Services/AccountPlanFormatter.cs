namespace CodexProfileOverlay.Core.Services;

public static class AccountPlanFormatter
{
    public static string? Normalize(string? planName) => planName?.Trim().ToLowerInvariant() switch
    {
        "plus" => "Plus",
        "pro" => "Pro",
        "free" => "Free",
        "team" => "Team",
        "business" => "Business",
        "enterprise" => "Enterprise",
        "edu" => "Edu",
        "go" => "Go",
        "prolite" or "pro_lite" or "pro lite" => "Pro Lite",
        "promax" or "pro_max" or "pro max" => "Pro Max",
        "self_serve_business_prolite" or "business pro lite" => "Business Pro Lite",
        "self_serve_business_usage_based" => "Business",
        "ent26" or "enterprise_cbp_automation" or "enterprise_cbp_usage_based" => "Enterprise",
        "edu_plus" or "edu plus" => "Edu Plus",
        "edu_pro" or "edu pro" => "Edu Pro",
        _ => null,
    };
}
