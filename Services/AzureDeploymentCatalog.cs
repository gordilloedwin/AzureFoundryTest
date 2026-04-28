using AzureFoundryTest.Diagnostics;
using AzureFoundryTest.Services.Interfaces;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.CognitiveServices;

namespace AzureFoundryTest.Services;

public sealed class AzureDeploymentCatalog : IDeploymentCatalog
{
	private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

	private readonly string? _subscriptionId;
	private readonly string? _resourceGroup;
	private readonly string? _accountName;
	private readonly bool _armConfigured;
	private readonly TokenCredential _credential;
	private readonly IReadOnlyList<DeploymentInfo> _configuredFallback;
	private readonly ILogger<AzureDeploymentCatalog> _logger;
	private readonly SemaphoreSlim _refreshGate = new(1, 1);

	private IReadOnlyList<DeploymentInfo>? _cached;
	private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

	public AzureDeploymentCatalog(
		IConfiguration configuration,
		TokenCredential credential,
		ILogger<AzureDeploymentCatalog> logger)
	{
		_credential = credential;
		_logger = logger;

		_subscriptionId = configuration["AzureOpenAI:SubscriptionId"];
		_resourceGroup = configuration["AzureOpenAI:ResourceGroup"];
		_accountName = configuration["AzureOpenAI:AccountName"];
		if (string.IsNullOrWhiteSpace(_accountName))
		{
			// ?? won't fall through on "", only on null. appsettings.json ships with "" placeholders,
			// so explicitly treat whitespace/empty as "not set" before attempting derivation.
			_accountName = DeriveAccountFromEndpoint(configuration["AzureOpenAI:Endpoint"]);
		}

		_armConfigured =
			!string.IsNullOrWhiteSpace(_subscriptionId) &&
			!string.IsNullOrWhiteSpace(_resourceGroup) &&
			!string.IsNullOrWhiteSpace(_accountName);

		if (_armConfigured)
		{
			logger.LogInformation(
				"[deployment catalog] ARM SDK configured for account '{Account}' in RG '{ResourceGroup}'",
				_accountName, _resourceGroup);
		}
		else
		{
			List<string> missing = new();
			if (string.IsNullOrWhiteSpace(_subscriptionId)) missing.Add("SubscriptionId");
			if (string.IsNullOrWhiteSpace(_resourceGroup)) missing.Add("ResourceGroup");
			if (string.IsNullOrWhiteSpace(_accountName)) missing.Add("AccountName (or derivable Endpoint)");
			logger.LogWarning(
				"[deployment catalog] ARM SDK NOT configured — missing: {Missing}. Live discovery disabled; falling back to config allowlist.",
				string.Join(", ", missing));
		}

		// Fallback allowlist: AzureOpenAI:AllowedDeployments (array) if set, else the single DeploymentName.
		string[]? fromConfig = configuration.GetSection("AzureOpenAI:AllowedDeployments").Get<string[]>();
		if (fromConfig is { Length: > 0 })
		{
			_configuredFallback = fromConfig.Select(n => new DeploymentInfo(n, null, null, "config")).ToArray();
		}
		else if (configuration["AzureOpenAI:DeploymentName"] is { Length: > 0 } defaultName)
		{
			_configuredFallback = new[] { new DeploymentInfo(defaultName, null, null, "config") };
		}
		else
		{
			_configuredFallback = Array.Empty<DeploymentInfo>();
		}
	}

	public async Task<IReadOnlyList<DeploymentInfo>> ListAsync(CancellationToken cancellationToken = default)
	{
		if (_cached is not null && DateTimeOffset.UtcNow - _cachedAt < CacheTtl)
		{
			AppMetrics.CatalogLookup.Add(1, new KeyValuePair<string, object?>("result", "hit"));
			return _cached;
		}

		await _refreshGate.WaitAsync(cancellationToken);
		try
		{
			if (_cached is not null && DateTimeOffset.UtcNow - _cachedAt < CacheTtl)
			{
				// Another thread refreshed while we waited — value of double-checked locking, made measurable.
				AppMetrics.CatalogLookup.Add(1, new KeyValuePair<string, object?>("result", "coalesced"));
				return _cached;
			}

			IReadOnlyList<DeploymentInfo>? fromAzure = await TryFetchFromAzureAsync(cancellationToken);
			string source;
			if (fromAzure is { Count: > 0 })
			{
				_logger.LogInformation("[deployment catalog] loaded {Count} deployment(s) from Azure ARM", fromAzure.Count);
				_cached = fromAzure;
				source = "azure";
			}
			else
			{
				_logger.LogWarning(
					"[deployment catalog] ARM listing unavailable — falling back to {Count} config-declared deployment(s). " +
					"Ensure AzureOpenAI:SubscriptionId, :ResourceGroup, and :AccountName are set and the identity has " +
					"'Microsoft.CognitiveServices/accounts/deployments/read' (e.g. Cognitive Services User, Reader, or Contributor).",
					_configuredFallback.Count);
				_cached = _configuredFallback;
				source = "config";
			}

			_cachedAt = DateTimeOffset.UtcNow;
			AppMetrics.CatalogLookup.Add(1, new KeyValuePair<string, object?>("result", "refreshed"));
			AppMetrics.CatalogRefresh.Add(1, new KeyValuePair<string, object?>("source", source));
			return _cached;
		}
		finally
		{
			_refreshGate.Release();
		}
	}

	private async Task<IReadOnlyList<DeploymentInfo>?> TryFetchFromAzureAsync(CancellationToken cancellationToken)
	{
		if (!_armConfigured)
		{
			_logger.LogInformation(
				"[deployment catalog] ARM config incomplete — skipping live listing. " +
				"Set AzureOpenAI:SubscriptionId, :ResourceGroup, and (optionally) :AccountName to enable.");
			return null;
		}

		try
		{
			var armClient = new ArmClient(_credential);
			var accountId = CognitiveServicesAccountResource.CreateResourceIdentifier(
				_subscriptionId!, _resourceGroup!, _accountName!);
			var account = armClient.GetCognitiveServicesAccountResource(accountId);
			var deploymentCollection = account.GetCognitiveServicesAccountDeployments();

			List<DeploymentInfo> results = new();
			await foreach (var deployment in deploymentCollection.GetAllAsync(cancellationToken: cancellationToken))
			{
				string? name = deployment.Data.Name;
				if (string.IsNullOrWhiteSpace(name))
				{
					continue;
				}

				string? model = deployment.Data.Properties?.Model?.Name;
				string? status = deployment.Data.Properties?.ProvisioningState?.ToString();
				results.Add(new DeploymentInfo(name, model, status, "azure"));
			}

			return results;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "[deployment catalog] failed to query ARM deployment listing");
			return null;
		}
	}

	private static string? DeriveAccountFromEndpoint(string? endpoint)
	{
		if (string.IsNullOrWhiteSpace(endpoint) ||
			!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri))
		{
			return null;
		}

		// e.g. "aoai-copilot-forge.openai.azure.com" -> "aoai-copilot-forge"
		string host = uri.Host;
		int dot = host.IndexOf('.');
		return dot > 0 ? host[..dot] : host;
	}
}
