using Azure;
using Azure.AI.Projects;
using Azure.AI.TextAnalytics;
using Azure.Core;
using AzureFoundryTest.Models.Pii;
using AzureFoundryTest.Services.Interfaces;

namespace AzureFoundryTest.Services;

/// <summary>
/// PII service that resolves the Azure AI Language endpoint directly from the Azure AI Foundry
/// project using the connection named by <c>AzureFoundry:PiiConnectionName</c> (defaults to
/// <c>"Azure-Language-Text-PII-redaction"</c>).
///
/// Flow:
///   1. AIProjectClient.Connections.GetConnectionWithCredentialsAsync(connectionName)
///      → Foundry returns the Language service endpoint + credentials for that exact model.
///   2. A TextAnalyticsClient is constructed from those resolved values.
///   3. RecognizePiiEntitiesAsync is called on the resolved endpoint.
///
/// This means <c>AzureFoundry:PiiConnectionName</c> is what tells Foundry which deployed model
/// to use — it acts as the model selector inside the Foundry project.
/// </summary>
public sealed class FoundryPiiService : IPiiService
{
    private readonly AIProjectClient _projectClient;
    private readonly TokenCredential _credential;
    private readonly string _connectionName;
    private readonly ILogger<FoundryPiiService> _logger;

    public FoundryPiiService(
        AIProjectClient projectClient,
        TokenCredential credential,
        IConfiguration config,
        ILogger<FoundryPiiService> logger)
    {
        _projectClient = projectClient;
        _credential = credential;
        _connectionName = config["AzureFoundry:PiiConnectionName"] ?? "Azure-Language-Text-PII-redaction";
        _logger = logger;
    }

    public async Task<PiiAnalysisResult> AnalyzeAsync(
        string text,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "[foundry-pii] resolving Language connection '{Connection}' from Foundry project",
                _connectionName);

            AIProjectConnection connection = await _projectClient.Connections
                .GetConnectionAsync(_connectionName, includeCredentials: true, cancellationToken);

            _logger.LogInformation(
                "[foundry-pii] resolved endpoint={Target}, type={Type}",
                connection.Target, connection.Type);

            TextAnalyticsClient languageClient = BuildLanguageClient(connection);

            RecognizePiiEntitiesOptions options = new() { DomainFilter = PiiEntityDomain.None };

            Response<PiiEntityCollection> response = await languageClient.RecognizePiiEntitiesAsync(
                text, language, options, cancellationToken);

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

            _logger.LogInformation("[foundry-pii] detected {Count} PII entities", entities.Count);

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
            _logger.LogWarning(ex, "[foundry-pii] PII analysis failed");

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

    private TextAnalyticsClient BuildLanguageClient(AIProjectConnection connection)
    {
        Uri endpoint = new(connection.Target);

        // If Foundry vends an API key for this connection, use it directly.
        // Otherwise fall through to the ambient DefaultAzureCredential (Entra ID).
        if (connection.Credentials is AIProjectConnectionApiKeyCredential apiKey)
        {
            _logger.LogDebug("[foundry-pii] using API key credential from Foundry connection");
            return new TextAnalyticsClient(endpoint, new AzureKeyCredential(apiKey.ApiKey));
        }

        _logger.LogDebug("[foundry-pii] using DefaultAzureCredential for Foundry connection");
        return new TextAnalyticsClient(endpoint, _credential);
    }
}
