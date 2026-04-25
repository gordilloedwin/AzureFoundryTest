using AzureFoundryTest.Services.Interfaces;
using Azure.Core;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AzureFoundryTest.Services;

public sealed class AzureDeploymentCatalog : IDeploymentCatalog
{
	private const string CognitiveServicesScope = "https://cognitiveservices.azure.com/.default";
	private const string DeploymentsApiVersion = "2024-10-21";
	private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

	private readonly Uri _endpoint;
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
		string endpoint = configuration["AzureOpenAI:Endpoint"]
			?? throw new InvalidOperationException("Configuration value 'AzureOpenAI:Endpoint' is required.");

		_endpoint = new Uri(endpoint);
		_credential = credential;
		_http = httpFactory.CreateClient(nameof(AzureDeploymentCatalog));
		_logger = logger;

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
			return _cached;
		}

		await _refreshGate.WaitAsync(cancellationToken);
		try
		{
			if (_cached is not null && DateTimeOffset.UtcNow - _cachedAt < CacheTtl)
			{
				return _cached;
			}

			IReadOnlyList<DeploymentInfo>? fromAzure = await TryFetchFromAzureAsync(cancellationToken);
			if (fromAzure is { Count: > 0 })
			{
				_logger.LogInformation("[deployment catalog] loaded {Count} deployment(s) from Azure", fromAzure.Count);
				_cached = fromAzure;
			}
			else
			{
				_logger.LogWarning(
					"[deployment catalog] Azure listing unavailable — falling back to {Count} config-declared deployment(s). " +
					"To enable live discovery, grant the running identity the 'Cognitive Services User' role (or higher) on the resource.",
					_configuredFallback.Count);
				_cached = _configuredFallback;
			}

			_cachedAt = DateTimeOffset.UtcNow;
			return _cached;
		}
		finally
		{
			_refreshGate.Release();
		}
	}

	private async Task<IReadOnlyList<DeploymentInfo>?> TryFetchFromAzureAsync(CancellationToken cancellationToken)
	{
		try
		{
			AccessToken token = await _credential.GetTokenAsync(
				new TokenRequestContext(new[] { CognitiveServicesScope }),
				cancellationToken);

			Uri url = new(_endpoint, $"openai/deployments?api-version={DeploymentsApiVersion}");
			using HttpRequestMessage request = new(HttpMethod.Get, url);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

			using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning(
					"[deployment catalog] GET {Url} returned {Status}. Identity may lack deployment-listing permission.",
					url, (int)response.StatusCode);
				return null;
			}

			await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
			using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

			if (!doc.RootElement.TryGetProperty("data", out JsonElement dataEl) || dataEl.ValueKind != JsonValueKind.Array)
			{
				return null;
			}

			List<DeploymentInfo> results = new();
			foreach (JsonElement item in dataEl.EnumerateArray())
			{
				string? id = item.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
				if (string.IsNullOrWhiteSpace(id))
				{
					continue;
				}

				string? model = item.TryGetProperty("model", out JsonElement modelEl) ? modelEl.GetString() : null;
				string? status = item.TryGetProperty("status", out JsonElement statusEl) ? statusEl.GetString() : null;

				results.Add(new DeploymentInfo(id, model, status, "azure"));
			}

			return results;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "[deployment catalog] failed to query Azure deployment listing");
			return null;
		}
	}
}
