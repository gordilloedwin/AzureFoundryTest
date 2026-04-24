using Microsoft.Extensions.AI;
using System.Diagnostics;

namespace AzureFoundryTest.Middleware;

public sealed class TracingChatClient : DelegatingChatClient
{
	public static readonly ActivitySource ActivitySource = new("AzureFoundryTest.Chat");

	public TracingChatClient(IChatClient innerClient) : base(innerClient)
	{
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

		try
		{
			ChatResponse response = await base.GetResponseAsync(messages, options, cancellationToken);

			activity?.SetTag("gen_ai.response.id", response.ResponseId);
			activity?.SetTag("gen_ai.response.model", response.ModelId);
			activity?.SetTag("gen_ai.response.finish_reason", response.FinishReason?.ToString());
			activity?.SetTag("gen_ai.usage.input_tokens", response.Usage?.InputTokenCount);
			activity?.SetTag("gen_ai.usage.output_tokens", response.Usage?.OutputTokenCount);
			activity?.SetStatus(ActivityStatusCode.Ok);

			return response;
		}
		catch (Exception ex)
		{
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			activity?.SetTag("exception.type", ex.GetType().FullName);
			activity?.SetTag("exception.message", ex.Message);
			throw;
		}
	}
}
