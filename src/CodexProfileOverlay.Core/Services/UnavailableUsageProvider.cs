namespace CodexProfileOverlay.Core.Models;

public sealed class UnavailableUsageProvider : IUsageProvider
{
    public UsageProviderCapability Capability => UsageProviderCapability.Unavailable;

    public Task<UsageSnapshot?> GetUsageAsync(string profileDirectory, CancellationToken cancellationToken)
    {
        return Task.FromResult<UsageSnapshot?>(null);
    }
}