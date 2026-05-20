using AzureFoundryTest.Models.Pii;

namespace AzureFoundryTest.Services.Interfaces;

public interface IPiiService
{
    Task<PiiAnalysisResult> AnalyzeAsync(string text, string? language = null, CancellationToken cancellationToken = default);
}