using AzureFoundryTest.Diagnostics;
using AzureFoundryTest.Middleware;
using AzureFoundryTest.Services.Interfaces;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;

namespace AzureFoundryTest.Services;

public sealed class ChatClientFactory : IChatClientFactory
{
	
	private readonly AzureOpenAIClient _azureClient;
	private readonly IDeploymentCatalog _catalog;
	private readonly IServiceProvider _services;
	private readonly string _defaultDeployment;
	private readonly ConcurrentDictionary<string, IChatClient> _cache =
		new(StringComparer.OrdinalIgnoreCase);

	public ChatClientFactory(
		IConfiguration configuration,
		AzureOpenAIClient azureClient,
		IDeploymentCatalog catalog,
		IServiceProvider services)
	{
		_defaultDeployment = configuration["AzureOpenAI:DeploymentName"]
			?? throw new InvalidOperationException("Configuration value 'AzureOpenAI:DeploymentName' is required.");

		_azureClient = azureClient;
		_catalog = catalog;
		_services = services;

		// Observable gauge — read on each metric export. No allocation per chat call.
		AppMetrics.Meter.CreateObservableGauge<int>(
			"agent.client_factory.size",
			() => _cache.Count,
			description: "Number of cached per-deployment IChatClient instances currently held by the factory.");
	}

	public async Task<IChatClient> GetAsync(string? deployment, CancellationToken cancellationToken = default)
	{
		string target = string.IsNullOrWhiteSpace(deployment) ? _defaultDeployment : deployment!;

		IReadOnlyList<DeploymentInfo> allowed = await _catalog.ListAsync(cancellationToken);
		bool isAllowed = allowed.Any(d => d.Name.Equals(target, StringComparison.OrdinalIgnoreCase));
		if (!isAllowed)
		{
			string known = allowed.Count == 0 ? "(none)" : string.Join(", ", allowed.Select(d => d.Name));
			throw new ArgumentException(
				$"Deployment '{target}' is not in the allowed set. Known deployments: [{known}].",
				nameof(deployment));
		}

		// Lazy-build + cache per deployment. Each cached client carries its own middleware pipeline,
		// so tracing/logging wrapping happens once per deployment and is reused across calls.
		//
		// Pipeline order (outermost first): UseOpenTelemetry → TracingChatClient → inner Azure client.
		// .UseOpenTelemetry() emits the OTel GenAI semantic-convention spans + metrics
		// (gen_ai.client.operation.duration, gen_ai.client.token.usage). Default source/meter name is
		// "Experimental.Microsoft.Extensions.AI" — both must be subscribed to in Program.cs.
		// EnableSensitiveData stays at its default (false): no prompt/completion content captured,
		// which keeps central observability data-sovereignty-compliant.
		return _cache.GetOrAdd(target, name =>
		{
			IChatClient inner = _azureClient.GetChatClient(name).AsIChatClient();
			return new ChatClientBuilder(inner)
				.UseOpenTelemetry(loggerFactory: _services.GetRequiredService<ILoggerFactory>())
				.Use(c => ActivatorUtilities.CreateInstance<TracingChatClient>(_services, c))
				.Build();
		});
	}
}
