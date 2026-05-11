using AzureFoundryTest.Models.Sentiment;

namespace AzureFoundryTest.Services.Interfaces;

public interface ISentimentService
{
    Task<SentimentResult> AnalyzeAsync(string text, string? deployment = null, CancellationToken cancellationToken = default);
}
