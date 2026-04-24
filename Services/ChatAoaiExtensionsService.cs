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

	private readonly IChatClient _chatClient;

	// IChatClient comes from the DI pipeline configured in Program.cs (AddChatClient + .Use(...)).
	// The middleware stack (tracing, logging, caching, etc.) is transparent to this service —
	// it just calls GetResponseAsync and the wrappers do their work around it.
	public ChatAoaiExtensionsService(IChatClient chatClient)
	{
		_chatClient = chatClient;
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
		string pretty = JsonSerializer.Serialize(response, PrettyJson);

		Directory.CreateDirectory(Path.GetDirectoryName(ResponseFilePath)!);
		await File.WriteAllTextAsync(ResponseFilePath, pretty, cancellationToken);
	}
}
