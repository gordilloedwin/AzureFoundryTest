namespace AzureFoundryTest.Models.Pii;

public sealed record PiiAnalysisResult
{
    public string Text { get; init; } = string.Empty;

    public string RedactedText { get; init; } = string.Empty;

    public int EntityCount { get; init; }

    public bool Succeeded { get; init; }

    public string? Error { get; init; }

    public IReadOnlyList<PiiEntityResult> Entities { get; init; } = [];
}

public sealed record PiiEntityResult
{
    public string Text { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string? SubCategory { get; init; }

    public int Offset { get; init; }

    public int Length { get; init; }

    public double ConfidenceScore { get; init; }
}