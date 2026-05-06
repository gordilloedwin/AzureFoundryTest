using AzureFoundryTest.Models.Sentiment;

namespace AzureFoundryTest.Services.Interfaces;

public interface ISentimentService
{
    Task<SentimentResult> AnalyzeAsync(string text, CancellationToken cancellationToken = default);
}
