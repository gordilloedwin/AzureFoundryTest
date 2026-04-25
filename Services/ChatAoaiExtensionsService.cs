using AzureFoundryTest.Services.Interfaces;
using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureFoundryTest.Services;

public class ChatAoaiExtensionsService : IChatService
{
	private const string ResponseFilePath = "responses/ChatAoaiExtensionsService.json";

	private static readonly JsonSerializerOptions PrettyJson = new()
	{
		WriteIndented = true,
		ReferenceHandler = ReferenceHandler.IgnoreCycles,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	private readonly IChatClientFactory _clientFactory;

	// The factory hands back a cached-per-deployment IChatClient already wrapped
	// with the middleware pipeline configured in Program.cs.
	public ChatAoaiExtensionsService(IChatClientFactory clientFactory)
	{
		_clientFactory = clientFactory;
	}

	public async Task<string> AskAgentAsync(string input, string? deployment = null, CancellationToken cancellationToken = default)
	{
		IChatClient chatClient = await _clientFactory.GetAsync(deployment, cancellationToken);

		List<ChatMessage> messages =
		[
			new ChatMessage(ChatRole.System, "You are a helpful assistant."),
			new ChatMessage(ChatRole.User, input)
		];

		ChatResponse response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);

		await WriteResponseAsync(response, cancellationToken);

		string modelResponse = response.Text;

		return string.IsNullOrWhiteSpace(modelResponse)
			? "No response was returned by the model."
			: modelResponse;
	}

	private static async Task WriteResponseAsync(ChatResponse response, CancellationToken cancellationToken)
	{
		string pretty = JsonSerializer.Serialize(response, PrettyJson);

		Directory.CreateDirectory(Path.GetDirectoryName(ResponseFilePath)!);
		await File.WriteAllTextAsync(ResponseFilePath, pretty, cancellationToken);
	}
}
