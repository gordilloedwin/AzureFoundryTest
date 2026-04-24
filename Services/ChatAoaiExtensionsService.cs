using AzureFoundryTest.Services.Interfaces;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureFoundryTest.Services;

public class ChatAoaiExtensionsService : IChatService
{
	private const string EndpointConfigKey = "AzureOpenAI:Endpoint";
	private const string DeploymentConfigKey = "AzureOpenAI:DeploymentName";
	private const string ResponseFilePath = "responses/ChatAoaiExtensionsService.json";

	private static readonly JsonSerializerOptions PrettyJson = new()
	{
		WriteIndented = true,
		ReferenceHandler = ReferenceHandler.IgnoreCycles,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	private readonly IChatClient _chatClient;

	public ChatAoaiExtensionsService(IConfiguration configuration)
	{
		string endpoint = configuration[EndpointConfigKey]
			?? throw new InvalidOperationException($"Configuration value '{EndpointConfigKey}' is required.");

		string deploymentName = configuration[DeploymentConfigKey]
			?? throw new InvalidOperationException($"Configuration value '{DeploymentConfigKey}' is required.");

		AzureOpenAIClient azureOpenAIClient = new(new Uri(endpoint), new DefaultAzureCredential());
		_chatClient = azureOpenAIClient.GetChatClient(deploymentName).AsIChatClient();
	}

	public async Task<string> AskAgentAsync(string input, CancellationToken cancellationToken = default)
	{
		List<ChatMessage> messages =
		[
			new ChatMessage(ChatRole.System, "You are a helpful assistant."),
			new ChatMessage(ChatRole.User, input)
		];

		ChatResponse response = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);

		await WriteResponseAsync(response, cancellationToken);

		string modelResponse = response.Text;

		return string.IsNullOrWhiteSpace(modelResponse)
			? "No response was returned by the model."
			: modelResponse;
	}

	private static async Task WriteResponseAsync(ChatResponse response, CancellationToken cancellationToken)
	{
		// ChatResponse is a plain POCO — System.Text.Json produces the M.E.AI-normalized shape directly.
		// IgnoreCycles guards against RawRepresentation pointing back into inspectable object graphs.
		string pretty = JsonSerializer.Serialize(response, PrettyJson);

		Directory.CreateDirectory(Path.GetDirectoryName(ResponseFilePath)!);
		await File.WriteAllTextAsync(ResponseFilePath, pretty, cancellationToken);
	}
}
