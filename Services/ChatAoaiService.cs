using AzureFoundryTest.Services.Interfaces;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;

namespace AzureFoundryTest.Services;

public class ChatAoaiService : IChatService
{
	private const string EndpointConfigKey = "AzureOpenAI:Endpoint";
	private const string DeploymentConfigKey = "AzureOpenAI:DeploymentName";

	private readonly ChatClient _chatClient;

	public ChatAoaiService(IConfiguration configuration)
	{
		string endpoint = configuration[EndpointConfigKey]
			?? throw new InvalidOperationException($"Configuration value '{EndpointConfigKey}' is required.");

		string deploymentName = configuration[DeploymentConfigKey]
			?? throw new InvalidOperationException($"Configuration value '{DeploymentConfigKey}' is required.");

		AzureOpenAIClient azureOpenAIClient = new(new Uri(endpoint), new DefaultAzureCredential());
		_chatClient = azureOpenAIClient.GetChatClient(deploymentName);
	}

	public async Task<string> AskAgentAsync(string input, CancellationToken cancellationToken = default)
	{
		List<ChatMessage> messages =
		[
			new SystemChatMessage("You are a helpful assistant."),
			new UserChatMessage(input)
		];

		var response = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);

		string modelResponse = string.Concat(response.Value.Content.Select(part => part.Text));

		return string.IsNullOrWhiteSpace(modelResponse)
			? "No response was returned by the model."
			: modelResponse;
	}
}
