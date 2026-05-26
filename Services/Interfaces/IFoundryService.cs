namespace AzureFoundryTest.Services.Interfaces;

public interface IFoundryService
{
    Task<string> AskAsync(string input, string? model = null, CancellationToken cancellationToken = default);
}
