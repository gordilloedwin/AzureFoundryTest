namespace AzureFoundryTest.Models.Sentiment;

public class SentimentResult
{
    // Legacy summary fields kept for backward compatibility.
    public string Sentiment { get; init; } = string.Empty;
    public double PositiveScore { get; init; }
    public double NeutralScore { get; init; }
    public double NegativeScore { get; init; }

    public string Text { get; init; } = string.Empty;
    public string ConsensusSentiment { get; init; } = string.Empty;
    public bool IsConsistent { get; init; }
    public SentimentProviderResult AzureLanguage { get; init; } = new();
    public SentimentProviderResult AzureOpenAI { get; init; } = new();
}

public class SentimentProviderResult
{
    public string Provider { get; init; } = string.Empty;
    public bool Succeeded { get; init; }
    public string Sentiment { get; init; } = string.Empty;
    public double? PositiveScore { get; init; }
    public double? NeutralScore { get; init; }
    public double? NegativeScore { get; init; }
    public string? Explanation { get; init; }
    public string? Error { get; init; }
}
