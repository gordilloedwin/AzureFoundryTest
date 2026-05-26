#pragma warning disable OPENAI001

using System.ClientModel.Primitives;
using System.Text.Json;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using AzureFoundryTest.Services.Interfaces;
using OpenAI.Responses;

namespace AzureFoundryTest.Services;

public sealed class FoundryService : IFoundryService
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    private readonly AIProjectClient _projectClient;
    private readonly string _defaultModel;
    private readonly ILogger<FoundryService> _logger;

    public FoundryService(AIProjectClient projectClient, IConfiguration config, ILogger<FoundryService> logger)
    {
        _projectClient = projectClient;
        _defaultModel = config["AzureFoundry:Model"] ?? "gpt-4o-mini";
        _logger = logger;
    }

    public async Task<string> AskAsync(string input, string? model = null, CancellationToken cancellationToken = default)
    {
        string resolvedModel = !string.IsNullOrWhiteSpace(model) ? model : _defaultModel;

        _logger.LogInformation("[foundry service] invoking Responses API — model={Model}", resolvedModel);

        ProjectResponsesClient responseClient = _projectClient.ProjectOpenAIClient.GetProjectResponsesClientForModel(resolvedModel);
        ResponseResult response = await responseClient.CreateResponseAsync(input, cancellationToken: cancellationToken);

        string output = response.GetOutputText();

        _logger.LogInformation("[foundry service] response received — length={Length}", output.Length);

        await SaveResponseAsync(response);

        return output;
    }

    private async Task SaveResponseAsync(ResponseResult response)
    {
        try
        {
            BinaryData raw = ModelReaderWriter.Write(response, ModelReaderWriterOptions.Json);
            string pretty = JsonSerializer.Serialize(
                JsonDocument.Parse(raw).RootElement,
                IndentedJson);

            string path = Path.Combine("responses", "FoundryResponse.json");
            Directory.CreateDirectory("responses");
            await File.WriteAllTextAsync(path, pretty);

            _logger.LogInformation("[foundry service] raw response saved to {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[foundry service] could not save raw response");
        }
    }
}
