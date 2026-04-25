using AzureFoundryTest.Diagnostics;
using Microsoft.Extensions.AI;
using System.Diagnostics;

namespace AzureFoundryTest.Middleware;

public sealed class TracingChatClient : DelegatingChatClient
{
	public static readonly ActivitySource ActivitySource = new("AzureFoundryTest.Chat");

	private readonly ILogger<TracingChatClient> _logger;

	public TracingChatClient(IChatClient innerClient, ILogger<TracingChatClient> logger)
		: base(innerClient)
	{
		_logger = logger;
	}

	public override async Task<ChatResponse> GetResponseAsync(
		IEnumerable<ChatMessage> messages,
		ChatOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		using Activity? activity = ActivitySource.StartActivity("chat.get_response", ActivityKind.Client);

		// Tag names follow the OpenTelemetry GenAI semantic conventions (gen_ai.*)
		// so the output is readable by any conformant observability backend.
		activity?.SetTag("gen_ai.system", "azure.openai");
		activity?.SetTag("gen_ai.request.model", options?.ModelId);

		_logger.LogInformation(
			"[chat middleware] outgoing request model={Model}",
			options?.ModelId ?? "<default>");

		try
		{
			ChatResponse response = await base.GetResponseAsync(messages, options, cancellationToken);

			string finishReason = response.FinishReason?.ToString() ?? "unknown";
			AppMetrics.FinishReason.Add(1, new KeyValuePair<string, object?>("reason", finishReason));

			activity?.SetTag("gen_ai.response.id", response.ResponseId);
			activity?.SetTag("gen_ai.response.model", response.ModelId);
			activity?.SetTag("gen_ai.response.finish_reason", finishReason);
			activity?.SetTag("gen_ai.usage.input_tokens", response.Usage?.InputTokenCount);
			activity?.SetTag("gen_ai.usage.output_tokens", response.Usage?.OutputTokenCount);
			activity?.SetStatus(ActivityStatusCode.Ok);

			_logger.LogInformation(
				"[chat middleware] response id={ResponseId} model={Model} finish={Finish} tokens={Input}/{Output}",
				response.ResponseId,
				response.ModelId,
				response.FinishReason?.ToString(),
				response.Usage?.InputTokenCount,
				response.Usage?.OutputTokenCount);

			return response;
		}
		catch (Exception ex)
		{
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			activity?.SetTag("exception.type", ex.GetType().FullName);
			activity?.SetTag("exception.message", ex.Message);

			_logger.LogError(ex, "[chat middleware] call failed");

			throw;
		}
	}
}
