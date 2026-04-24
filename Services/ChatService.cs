using AzureFoundryTest.Services.Interfaces;

namespace AzureFoundryTest.Services;

public class ChatService : IChatService
{
	public async Task<string> AskAgentAsync(string input, CancellationToken cancellationToken = default)
	{
		await Task.CompletedTask;

		return $"Agent response: {input}";
	}
}
