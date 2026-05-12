using AzureFoundryTest.Models.Vision;

namespace AzureFoundryTest.Services.Interfaces;

public interface IVisionService
{
    Task<VisionAnalysisResult> AnalyzeImageUrlAsync(Uri imageUrl, bool genderNeutralCaption, CancellationToken cancellationToken = default);

    Task<VisionAnalysisResult> AnalyzeImageStreamAsync(Stream imageStream, bool genderNeutralCaption, CancellationToken cancellationToken = default);

    Task<VisionAnalysisResult> ReadImageUrlAsync(Uri imageUrl, CancellationToken cancellationToken = default);

    Task<VisionAnalysisResult> ReadImageStreamAsync(Stream imageStream, CancellationToken cancellationToken = default);
}