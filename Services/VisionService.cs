using Azure.AI.Vision.ImageAnalysis;
using AzureFoundryTest.Models.Vision;
using AzureFoundryTest.Services.Interfaces;
using System.ClientModel.Primitives;
using System.Text.Json;

namespace AzureFoundryTest.Services;

public sealed class VisionService : IVisionService
{
    private const string AnalyzeResponseFilePath = "responses/VisionAnalyze.json";
    private const string ReadResponseFilePath = "responses/VisionRead.json";

    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true
    };

    private static readonly VisualFeatures AnalyzeFeatures =
        VisualFeatures.Caption |
        VisualFeatures.Tags |
        VisualFeatures.Objects |
        VisualFeatures.DenseCaptions |
        VisualFeatures.People;

    private readonly ImageAnalysisClient _imageAnalysisClient;
    private readonly ILogger<VisionService> _logger;

    public VisionService(ImageAnalysisClient imageAnalysisClient, ILogger<VisionService> logger)
    {
        _imageAnalysisClient = imageAnalysisClient;
        _logger = logger;
    }

    public async Task<VisionAnalysisResult> AnalyzeImageUrlAsync(Uri imageUrl, bool genderNeutralCaption, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[vision service] analyzing remote image {ImageUrl}", imageUrl);

        ImageAnalysisResult result = await _imageAnalysisClient.AnalyzeAsync(
            imageUrl,
            AnalyzeFeatures,
            new ImageAnalysisOptions { GenderNeutralCaption = genderNeutralCaption },
            cancellationToken);

        return await CreateResultAsync("analyze-url", "url", imageUrl.ToString(), result, AnalyzeResponseFilePath, cancellationToken);
    }

    public async Task<VisionAnalysisResult> AnalyzeImageStreamAsync(Stream imageStream, bool genderNeutralCaption, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[vision service] analyzing uploaded image stream");

        ImageAnalysisResult result = await _imageAnalysisClient.AnalyzeAsync(
            BinaryData.FromStream(imageStream),
            AnalyzeFeatures,
            new ImageAnalysisOptions { GenderNeutralCaption = genderNeutralCaption },
            cancellationToken);

        return await CreateResultAsync("analyze-upload", "upload", "uploaded-file", result, AnalyzeResponseFilePath, cancellationToken);
    }

    public async Task<VisionAnalysisResult> ReadImageUrlAsync(Uri imageUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[vision service] extracting text from remote image {ImageUrl}", imageUrl);

        ImageAnalysisResult result = await _imageAnalysisClient.AnalyzeAsync(
            imageUrl,
            VisualFeatures.Read,
            new ImageAnalysisOptions(),
            cancellationToken);

        return await CreateResultAsync("read-url", "url", imageUrl.ToString(), result, ReadResponseFilePath, cancellationToken);
    }

    public async Task<VisionAnalysisResult> ReadImageStreamAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[vision service] extracting text from uploaded image stream");

        ImageAnalysisResult result = await _imageAnalysisClient.AnalyzeAsync(
            BinaryData.FromStream(imageStream),
            VisualFeatures.Read,
            new ImageAnalysisOptions(),
            cancellationToken);

        return await CreateResultAsync("read-upload", "upload", "uploaded-file", result, ReadResponseFilePath, cancellationToken);
    }

    private static async Task<VisionAnalysisResult> CreateResultAsync(
        string operation,
        string sourceKind,
        string source,
        ImageAnalysisResult result,
        string responseFilePath,
        CancellationToken cancellationToken)
    {
        BinaryData raw = ModelReaderWriter.Write(result);
        using JsonDocument document = JsonDocument.Parse(raw);
        string pretty = JsonSerializer.Serialize(document.RootElement, PrettyJson);

        Directory.CreateDirectory(Path.GetDirectoryName(responseFilePath)!);
        await File.WriteAllTextAsync(responseFilePath, pretty, cancellationToken);

        return new VisionAnalysisResult
        {
            Operation = operation,
            SourceKind = sourceKind,
            Source = source,
            ModelVersion = result.ModelVersion,
            Caption = result.Caption?.Text,
            CaptionConfidence = result.Caption?.Confidence,
            TextLines = ExtractTextLines(document.RootElement),
            RawJson = pretty,
        };
    }

    private static IReadOnlyList<string> ExtractTextLines(JsonElement root)
    {
        if (!TryGetPropertyIgnoreCase(root, "read", out JsonElement readElement) ||
            readElement.ValueKind != JsonValueKind.Object ||
            !TryGetPropertyIgnoreCase(readElement, "blocks", out JsonElement blocksElement) ||
            blocksElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<string> lines = [];
        foreach (JsonElement block in blocksElement.EnumerateArray())
        {
            if (!TryGetPropertyIgnoreCase(block, "lines", out JsonElement blockLines) ||
                blockLines.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement line in blockLines.EnumerateArray())
            {
                if (TryGetPropertyIgnoreCase(line, "text", out JsonElement textElement) &&
                    textElement.ValueKind == JsonValueKind.String)
                {
                    string? text = textElement.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        lines.Add(text);
                    }
                }
            }
        }

        return lines;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (property.NameEquals(propertyName) ||
                property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}