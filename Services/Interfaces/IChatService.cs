namespace AzureFoundryTest.Services.Interfaces;

public interface IChatService
{
	// deployment is optional. When null/empty, the service uses its configured default.
	// ChatAoaiService ignores non-null values (native SDK binds deployment at startup).
	// ChatAoaiExtensionsService honors it by resolving a per-deployment IChatClient via the factory.
	Task<string> AskAgentAsync(string input, string? deployment = null, CancellationToken cancellationToken = default);
}
