namespace AzureFoundryTest.Models.Vision;

public sealed record VisionAnalysisResult
{
    public string Operation { get; init; } = string.Empty;

    public string SourceKind { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string? ModelVersion { get; init; }

    public string? Caption { get; init; }

    public double? CaptionConfidence { get; init; }

    public IReadOnlyList<string> TextLines { get; init; } = [];

    public string RawJson { get; init; } = string.Empty;
}

public sealed record VisionFeatureInfo(string Name, string Description, string TypicalUseCase);

public sealed record VisionCapabilitiesResult
{
    public string ResourceNotes { get; init; } = string.Empty;

    public IReadOnlyList<string> SupportedEndpoints { get; init; } = [];

    public IReadOnlyList<string> SupportedFormats { get; init; } = [];

    public IReadOnlyList<string> SupportedLimits { get; init; } = [];

    public IReadOnlyList<VisionFeatureInfo> FeatureCatalog { get; init; } = [];
}