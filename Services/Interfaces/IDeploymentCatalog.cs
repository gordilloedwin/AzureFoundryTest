namespace AzureFoundryTest.Services.Interfaces;

public interface IDeploymentCatalog
{
	Task<IReadOnlyList<DeploymentInfo>> ListAsync(CancellationToken cancellationToken = default);
}

public sealed record DeploymentInfo(string Name, string? Model, string? Status, string Source);
