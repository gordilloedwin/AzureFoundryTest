using AzureFoundryTest.Services.Interfaces;
using Azure.AI.OpenAI;
using Azure.Core;
using OpenAI.Chat;
using System.ClientModel.Primitives;
using System.Text.Json;

namespace AzureFoundryTest.Services;

public class ChatAoaiService : IChatService
{
	private const string DeploymentConfigKey = "AzureOpenAI:DeploymentName";
	private const string ResponseFilePath = "responses/ChatAoaiService.json";

	private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };

	private readonly ChatClient _chatClient;
	private readonly string _boundDeployment;
	private readonly ILogger<ChatAoaiService> _logger;

	public ChatAoaiService(
		IConfiguration configuration,
		AzureOpenAIClient azureClient,
		ILogger<ChatAoaiService> logger)
	{
		_boundDeployment = configuration[DeploymentConfigKey]
			?? throw new InvalidOperationException($"Configuration value '{DeploymentConfigKey}' is required.");

		_chatClient = azureClient.GetChatClient(_boundDeployment);
		_logger = logger;
	}

	public async Task<string> AskAgentAsync(string input, string? deployment = null, CancellationToken cancellationToken = default)
	{
		// Native SDK path: the ChatClient is constructor-bound to one deployment. Honoring a
		// per-call deployment parameter would require building our own client-cache factory here
		// too — demonstrating the difference between "native SDK, static" and "M.E.AI, composable."
		if (!string.IsNullOrWhiteSpace(deployment) &&
			!deployment.Equals(_boundDeployment, StringComparison.OrdinalIgnoreCase))
		{
			_logger.LogWarning(
				"[aoai service] per-call deployment '{Requested}' ignored — native path is bound to '{Bound}'",
				deployment, _boundDeployment);
		}

		List<ChatMessage> messages =
		[
			new SystemChatMessage("You are a helpful assistant."),
			new UserChatMessage(input)
		];

		var response = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);


		await WriteResponseAsync(response.Value, cancellationToken);

		string modelResponse = string.Concat(response.Value.Content.Select(part => part.Text));

		return string.IsNullOrWhiteSpace(modelResponse)
			? "No response was returned by the model."
			: modelResponse;
	}

	private static async Task WriteResponseAsync(ChatCompletion completion, CancellationToken cancellationToken)
	{
		BinaryData raw = ModelReaderWriter.Write(completion);
		using JsonDocument doc = JsonDocument.Parse(raw);
		string pretty = JsonSerializer.Serialize(doc.RootElement, PrettyJson);

		Directory.CreateDirectory(Path.GetDirectoryName(ResponseFilePath)!);
		await File.WriteAllTextAsync(ResponseFilePath, pretty, cancellationToken);
	}
}
