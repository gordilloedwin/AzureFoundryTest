namespace AzureFoundryTest.Services.Interfaces;

public interface IChatService
{
	Task<string> AskAgentAsync(string input, CancellationToken cancellationToken = default);
}
