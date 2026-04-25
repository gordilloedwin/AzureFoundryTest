using AzureFoundryTest.Diagnostics;
using AzureFoundryTest.Services.Interfaces;
using Azure.Core;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AzureFoundryTest.Services;

public sealed class AzureDeploymentCatalog : IDeploymentCatalog
{
	// ARM (control plane) is the correct surface for listing deployments.
	// Data plane has no list-deployments endpoint in GA, hence the earlier 404.
	private const string ManagementScope = "https://management.azure.com/.default";
	private const string DeploymentsApiVersion = "2024-10-01";
	private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

	private readonly Uri? _armListUrl;
	private readonly TokenCredential _credential;
	private readonly HttpClient _http;
	private readonly IReadOnlyList<DeploymentInfo> _configuredFallback;
	private readonly ILogger<AzureDeploymentCatalog> _logger;
	private readonly SemaphoreSlim _refreshGate = new(1, 1);

	private IReadOnlyList<DeploymentInfo>? _cached;
	private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

	public AzureDeploymentCatalog(
		IConfiguration configuration,
		TokenCredential credential,
		IHttpClientFactory httpFactory,
		ILogger<AzureDeploymentCatalog> logger)
	{
		_credential = credential;
		_http = httpFactory.CreateClient(nameof(AzureDeploymentCatalog));
		_logger = logger;

		string? subscriptionId = configuration["AzureOpenAI:SubscriptionId"];
		string? resourceGroup = configuration["AzureOpenAI:ResourceGroup"];
		string? accountName = configuration["AzureOpenAI:AccountName"];
		if (string.IsNullOrWhiteSpace(accountName))
		{
			// ?? won't fall through on "", only on null. appsettings.json ships with "" placeholders,
			// so explicitly treat whitespace/empty as "not set" before attempting derivation.
			accountName = DeriveAccountFromEndpoint(configuration["AzureOpenAI:Endpoint"]);
		}

		if (!string.IsNullOrWhiteSpace(subscriptionId) &&
			!string.IsNullOrWhiteSpace(resourceGroup) &&
			!string.IsNullOrWhiteSpace(accountName))
		{
			_armListUrl = new Uri(
				$"https://management.azure.com/subscriptions/{subscriptionId}" +
				$"/resourceGroups/{resourceGroup}" +
				$"/providers/Microsoft.CognitiveServices/accounts/{accountName}" +
				$"/deployments?api-version={DeploymentsApiVersion}");
			logger.LogInformation(
				"[deployment catalog] ARM list URL configured for account '{Account}' in RG '{ResourceGroup}'",
				accountName, resourceGroup);
		}
		else
		{
			List<string> missing = new();
			if (string.IsNullOrWhiteSpace(subscriptionId)) missing.Add("SubscriptionId");
			if (string.IsNullOrWhiteSpace(resourceGroup)) missing.Add("ResourceGroup");
			if (string.IsNullOrWhiteSpace(accountName)) missing.Add("AccountName (or derivable Endpoint)");
			logger.LogWarning(
				"[deployment catalog] ARM list URL NOT configured — missing: {Missing}. Live discovery disabled; falling back to config allowlist.",
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
		if (_armListUrl is null)
		{
			_logger.LogInformation(
				"[deployment catalog] ARM config incomplete — skipping live listing. " +
				"Set AzureOpenAI:SubscriptionId, :ResourceGroup, and (optionally) :AccountName to enable.");
			return null;
		}

		try
		{
			AccessToken token = await _credential.GetTokenAsync(
				new TokenRequestContext(new[] { ManagementScope }),
				cancellationToken);

			using HttpRequestMessage request = new(HttpMethod.Get, _armListUrl);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

			using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				string body = await response.Content.ReadAsStringAsync(cancellationToken);
				_logger.LogWarning(
					"[deployment catalog] GET {Url} returned {Status}. Body: {Body}",
					_armListUrl, (int)response.StatusCode, body);
				return null;
			}

			await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
			using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

			// ARM list shape: { "value": [ { "name": "...", "properties": { "model": { "name": "..." }, "provisioningState": "..." } } ] }
			if (!doc.RootElement.TryGetProperty("value", out JsonElement valueEl) || valueEl.ValueKind != JsonValueKind.Array)
			{
				return null;
			}

			List<DeploymentInfo> results = new();
			foreach (JsonElement item in valueEl.EnumerateArray())
			{
				string? name = item.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
				if (string.IsNullOrWhiteSpace(name))
				{
					continue;
				}

				string? model = null;
				string? status = null;
				if (item.TryGetProperty("properties", out JsonElement propsEl))
				{
					if (propsEl.TryGetProperty("model", out JsonElement modelEl) &&
						modelEl.TryGetProperty("name", out JsonElement modelNameEl))
					{
						model = modelNameEl.GetString();
					}
					if (propsEl.TryGetProperty("provisioningState", out JsonElement stateEl))
					{
						status = stateEl.GetString();
					}
				}

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
