using Microsoft.Extensions.AI;

namespace AzureFoundryTest.Services.Interfaces;

public interface IChatClientFactory
{
	// Returns an IChatClient bound to the requested deployment. When deployment is null/empty,
	// returns the default. Throws ArgumentException if the deployment is not in the catalog's allowed set.
	Task<IChatClient> GetAsync(string? deployment, CancellationToken cancellationToken = default);
}
