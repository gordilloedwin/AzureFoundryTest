namespace AzureFoundryTest.Models.Sentiment;

public class SentimentResult
{
    public string Sentiment { get; init; } = string.Empty;
    public double PositiveScore { get; init; }
    public double NeutralScore { get; init; }
    public double NegativeScore { get; init; }
}
