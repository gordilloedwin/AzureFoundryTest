using Azure.AI.TextAnalytics;
using AzureFoundryTest.Models.Sentiment;
using AzureFoundryTest.Services.Interfaces;

namespace AzureFoundryTest.Services;

public class SentimentService(TextAnalyticsClient client) : ISentimentService
{
    public async Task<SentimentResult> AnalyzeAsync(string text, CancellationToken cancellationToken = default)
    {
        DocumentSentiment result = await client.AnalyzeSentimentAsync(text, cancellationToken: cancellationToken);

        return new SentimentResult
        {
            Sentiment = result.Sentiment.ToString(),
            PositiveScore = result.ConfidenceScores.Positive,
            NeutralScore = result.ConfidenceScores.Neutral,
            NegativeScore = result.ConfidenceScores.Negative,
        };
    }
}
