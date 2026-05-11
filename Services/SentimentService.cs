using Azure.AI.TextAnalytics;
using Azure.AI.OpenAI;
using AzureFoundryTest.Models.Sentiment;
using AzureFoundryTest.Services.Interfaces;
using OpenAI.Chat;
using System.Globalization;
using System.Text.Json;

namespace AzureFoundryTest.Services;

public class SentimentService : ISentimentService
{
    private const string DeploymentConfigKey = "AzureOpenAI:DeploymentName";

    private readonly TextAnalyticsClient _textAnalyticsClient;
    private readonly AzureOpenAIClient _azureOpenAIClient;
    private readonly ILogger<SentimentService> _logger;
    private readonly string? _defaultDeployment;

    public SentimentService(
        TextAnalyticsClient textAnalyticsClient,
        AzureOpenAIClient azureOpenAIClient,
        IConfiguration configuration,
        ILogger<SentimentService> logger)
    {
        _textAnalyticsClient = textAnalyticsClient;
        _azureOpenAIClient = azureOpenAIClient;
        _logger = logger;
        _defaultDeployment = configuration[DeploymentConfigKey];
    }

    public async Task<SentimentResult> AnalyzeAsync(string text, string? deployment = null, CancellationToken cancellationToken = default)
    {
        Task<SentimentProviderResult> languageTask = AnalyzeWithAzureLanguageAsync(text, cancellationToken);
        Task<SentimentProviderResult> aoaiTask = AnalyzeWithAzureOpenAiAsync(text, deployment, cancellationToken);

        await Task.WhenAll(languageTask, aoaiTask);

        SentimentProviderResult languageResult = languageTask.Result;
        SentimentProviderResult aoaiResult = aoaiTask.Result;
        string consensus = ResolveConsensus(languageResult, aoaiResult);
        bool isConsistent = languageResult.Succeeded
            && aoaiResult.Succeeded
            && languageResult.Sentiment.Equals(aoaiResult.Sentiment, StringComparison.OrdinalIgnoreCase);

        SentimentProviderResult summarySource = languageResult.Succeeded ? languageResult : aoaiResult;

        return new SentimentResult
        {
            Text = text,
            Sentiment = summarySource.Sentiment,
            PositiveScore = summarySource.PositiveScore ?? 0,
            NeutralScore = summarySource.NeutralScore ?? 0,
            NegativeScore = summarySource.NegativeScore ?? 0,
            ConsensusSentiment = consensus,
            IsConsistent = isConsistent,
            AzureLanguage = languageResult,
            AzureOpenAI = aoaiResult,
        };
    }

    private async Task<SentimentProviderResult> AnalyzeWithAzureLanguageAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            DocumentSentiment result = await _textAnalyticsClient.AnalyzeSentimentAsync(text, cancellationToken: cancellationToken);
            return new SentimentProviderResult
            {
                Provider = "AzureLanguage",
                Succeeded = true,
                Sentiment = result.Sentiment.ToString(),
                PositiveScore = result.ConfidenceScores.Positive,
                NeutralScore = result.ConfidenceScores.Neutral,
                NegativeScore = result.ConfidenceScores.Negative,
                Explanation = "Analyzed with Azure AI Language (Text Analytics).",
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[sentiment service] Azure Language sentiment analysis failed");
            return new SentimentProviderResult
            {
                Provider = "AzureLanguage",
                Succeeded = false,
                Sentiment = "Unknown",
                Error = ex.Message,
            };
        }
    }

    private async Task<SentimentProviderResult> AnalyzeWithAzureOpenAiAsync(string text, string? deployment, CancellationToken cancellationToken)
    {
        try
        {
            string selectedDeployment = string.IsNullOrWhiteSpace(deployment)
                ? _defaultDeployment ?? string.Empty
                : deployment;

            if (string.IsNullOrWhiteSpace(selectedDeployment))
            {
                return new SentimentProviderResult
                {
                    Provider = "AzureOpenAI",
                    Succeeded = false,
                    Sentiment = "Unknown",
                    Error = "Azure OpenAI deployment is not configured. Set AzureOpenAI:DeploymentName or provide model in the request.",
                };
            }

            ChatClient chatClient = _azureOpenAIClient.GetChatClient(selectedDeployment);
            List<ChatMessage> messages =
            [
                new SystemChatMessage("Classify sentiment for user text and return JSON only with keys sentiment, positiveScore, neutralScore, negativeScore, explanation. Sentiment must be one of Positive, Neutral, Negative, Mixed. Scores must be numbers from 0 to 1 that approximately sum to 1."),
                new UserChatMessage(text),
            ];

            ChatCompletion response = (await chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken)).Value;
            string content = string.Concat(response.Content.Select(part => part.Text));

            SentimentProviderResult parsed = TryParseOpenAiSentiment(content);
            return new SentimentProviderResult
            {
                Provider = "AzureOpenAI",
                Succeeded = parsed.Succeeded,
                Sentiment = parsed.Sentiment,
                PositiveScore = parsed.PositiveScore,
                NeutralScore = parsed.NeutralScore,
                NegativeScore = parsed.NegativeScore,
                Explanation = parsed.Explanation,
                Error = parsed.Succeeded ? null : parsed.Error ?? "Unable to parse Azure OpenAI sentiment response.",
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[sentiment service] Azure OpenAI sentiment analysis failed");
            return new SentimentProviderResult
            {
                Provider = "AzureOpenAI",
                Succeeded = false,
                Sentiment = "Unknown",
                Error = ex.Message,
            };
        }
    }

    private static SentimentProviderResult TryParseOpenAiSentiment(string content)
    {
        string jsonPayload = ExtractJson(content);

        try
        {
            using JsonDocument doc = JsonDocument.Parse(jsonPayload);
            JsonElement root = doc.RootElement;

            string sentiment = GetString(root, "sentiment", "Unknown");
            double? positive = GetDouble(root, "positiveScore");
            double? neutral = GetDouble(root, "neutralScore");
            double? negative = GetDouble(root, "negativeScore");
            string? explanation = GetNullableString(root, "explanation");

            return new SentimentProviderResult
            {
                Provider = "AzureOpenAI",
                Succeeded = !string.IsNullOrWhiteSpace(sentiment) && !sentiment.Equals("Unknown", StringComparison.OrdinalIgnoreCase),
                Sentiment = sentiment,
                PositiveScore = positive,
                NeutralScore = neutral,
                NegativeScore = negative,
                Explanation = explanation,
            };
        }
        catch (Exception ex)
        {
            return new SentimentProviderResult
            {
                Provider = "AzureOpenAI",
                Succeeded = false,
                Sentiment = "Unknown",
                Error = ex.Message,
            };
        }
    }

    private static string ExtractJson(string content)
    {
        string trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            int firstBrace = trimmed.IndexOf('{');
            int lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return trimmed[firstBrace..(lastBrace + 1)];
            }
        }

        return trimmed;
    }

    private static string ResolveConsensus(SentimentProviderResult languageResult, SentimentProviderResult aoaiResult)
    {
        if (languageResult.Succeeded && aoaiResult.Succeeded)
        {
            return languageResult.Sentiment.Equals(aoaiResult.Sentiment, StringComparison.OrdinalIgnoreCase)
                ? languageResult.Sentiment
                : $"Disagree({languageResult.Sentiment}|{aoaiResult.Sentiment})";
        }

        if (languageResult.Succeeded)
        {
            return languageResult.Sentiment;
        }

        if (aoaiResult.Succeeded)
        {
            return aoaiResult.Sentiment;
        }

        return "Unknown";
    }

    private static string GetString(JsonElement root, string propertyName, string defaultValue)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property))
        {
            return defaultValue;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return property.GetString() ?? defaultValue;
        }

        return defaultValue;
    }

    private static string? GetNullableString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }

    private static double? GetDouble(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.GetDouble();
        }

        if (property.ValueKind == JsonValueKind.String &&
            double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
        {
            return parsed;
        }

        return null;
    }
}
