namespace CodexProfileOverlay.Core.Models;

public interface IUsageProvider
{
    UsageProviderCapability Capability { get; }

    Task<UsageSnapshot?> GetUsageAsync(string profileDirectory, CancellationToken cancellationToken);
}