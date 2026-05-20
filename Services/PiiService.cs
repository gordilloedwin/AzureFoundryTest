using Azure.AI.TextAnalytics;
using AzureFoundryTest.Models.Pii;
using AzureFoundryTest.Services.Interfaces;

namespace AzureFoundryTest.Services;

public sealed class PiiService : IPiiService
{
    private readonly TextAnalyticsClient _textAnalyticsClient;
    private readonly ILogger<PiiService> _logger;

    public PiiService(TextAnalyticsClient textAnalyticsClient, ILogger<PiiService> logger)
    {
        _textAnalyticsClient = textAnalyticsClient;
        _logger = logger;
    }

    public async Task<PiiAnalysisResult> AnalyzeAsync(string text, string? language = null, CancellationToken cancellationToken = default)
    {
        try
        {
            RecognizePiiEntitiesOptions options = new()
            {
                DomainFilter = PiiEntityDomain.None,
            };

            var response = await _textAnalyticsClient.RecognizePiiEntitiesAsync(
                text,
                language,
                options,
                cancellationToken);

            PiiEntityCollection pii = response.Value;
            List<PiiEntityResult> entities =
            [
                .. pii.Select(entity => new PiiEntityResult
                {
                    Text = entity.Text,
                    Category = entity.Category.ToString(),
                    SubCategory = entity.SubCategory,
                    Offset = entity.Offset,
                    Length = entity.Length,
                    ConfidenceScore = entity.ConfidenceScore,
                })
            ];

            _logger.LogInformation("[pii service] detected {Count} entities", entities.Count);

            return new PiiAnalysisResult
            {
                Text = text,
                RedactedText = pii.RedactedText,
                EntityCount = entities.Count,
                Entities = entities,
                Succeeded = true,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[pii service] pii analysis failed");

            return new PiiAnalysisResult
            {
                Text = text,
                RedactedText = text,
                EntityCount = 0,
                Succeeded = false,
                Error = ex.Message,
            };
        }
    }
}