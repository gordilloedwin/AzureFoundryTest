using AzureFoundryTest.Services.Interfaces;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;

namespace AzureFoundryTest.Services;

public class ChatAoaiExtensionsService : IChatService
{
	private const string EndpointConfigKey = "AzureOpenAI:Endpoint";
	private const string DeploymentConfigKey = "AzureOpenAI:DeploymentName";

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

		string modelResponse = response.Text;

		return string.IsNullOrWhiteSpace(modelResponse)
			? "No response was returned by the model."
			: modelResponse;
	}
}
