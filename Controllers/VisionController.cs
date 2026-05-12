using AzureFoundryTest.Models.Vision;
using AzureFoundryTest.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AzureFoundryTest.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VisionController(ILogger<VisionController> logger) : ControllerBase
{
    [HttpGet("features")]
    public ActionResult<VisionCapabilitiesResult> GetFeatures()
    {
        return Ok(new VisionCapabilitiesResult
        {
            ResourceNotes = "Use a Computer Vision or Azure AI Vision resource with DefaultAzureCredential and Cognitive Services User access.",
            SupportedEndpoints =
            [
                "POST /api/vision/analyze/url",
                "POST /api/vision/analyze/upload",
                "POST /api/vision/read/url",
                "POST /api/vision/read/upload"
            ],
            SupportedFormats =
            [
                "JPEG",
                "PNG",
                "GIF",
                "BMP",
                "WEBP",
                "ICO",
                "TIFF",
                "MPO"
            ],
            SupportedLimits =
            [
                "Images must be publicly reachable for URL-based calls.",
                "File uploads should be under 20 MB.",
                "Image dimensions should be between 50x50 and 16000x16000 pixels.",
                "Caption and dense captions may require a GPU-supported region."
            ],
            FeatureCatalog =
            [
                new VisionFeatureInfo("Caption", "Generate a one-sentence description of the whole image.", "Quick scene summary and accessibility checks."),
                new VisionFeatureInfo("Tags", "Extract content tags for recognizable objects, scenery, living beings, and actions.", "Fast object discovery and broad scene classification."),
                new VisionFeatureInfo("Objects", "Detect physical objects and their bounding boxes.", "Find where things are in the image."),
                new VisionFeatureInfo("People", "Detect people and return bounding boxes.", "Locate people in photos or screenshots."),
                new VisionFeatureInfo("DenseCaptions", "Produce captions for the whole image and important regions.", "Explore detailed scene descriptions."),
                new VisionFeatureInfo("Read", "Extract printed or handwritten text using OCR.", "Text extraction from screenshots, documents, and signage.")
            ]
        });
    }

    [HttpPost("analyze/url")]
    public async Task<ActionResult<VisionAnalysisResult>> AnalyzeUrl(
        [FromServices] IVisionService visionService,
        [FromBody] VisionUrlRequest request,
        [FromQuery] bool genderNeutralCaption = true,
        CancellationToken cancellationToken = default)
    {
        if (!TryCreateImageUrl(request?.ImageUrl, out Uri imageUrl))
        {
            return BadRequest("ImageUrl must be a valid absolute URL.");
        }

        logger.LogInformation("[controller] vision analyze/url invoked for {ImageUrl}", imageUrl);

        VisionAnalysisResult result = await visionService.AnalyzeImageUrlAsync(imageUrl, genderNeutralCaption, cancellationToken);
        return Ok(result);
    }

    [HttpPost("analyze/upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<VisionAnalysisResult>> AnalyzeUpload(
        [FromServices] IVisionService visionService,
        IFormFile file,
        [FromQuery] bool genderNeutralCaption = true,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("File cannot be empty.");
        }

        logger.LogInformation("[controller] vision analyze/upload invoked for file {FileName} ({Length} bytes)", file.FileName, file.Length);

        await using Stream stream = file.OpenReadStream();
        VisionAnalysisResult result = await visionService.AnalyzeImageStreamAsync(stream, genderNeutralCaption, cancellationToken);
        return Ok(result);
    }

    [HttpPost("read/url")]
    public async Task<ActionResult<VisionAnalysisResult>> ReadUrl(
        [FromServices] IVisionService visionService,
        [FromBody] VisionUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryCreateImageUrl(request?.ImageUrl, out Uri imageUrl))
        {
            return BadRequest("ImageUrl must be a valid absolute URL.");
        }

        logger.LogInformation("[controller] vision read/url invoked for {ImageUrl}", imageUrl);

        VisionAnalysisResult result = await visionService.ReadImageUrlAsync(imageUrl, cancellationToken);
        return Ok(result);
    }

    [HttpPost("read/upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<VisionAnalysisResult>> ReadUpload(
        [FromServices] IVisionService visionService,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("File cannot be empty.");
        }

        logger.LogInformation("[controller] vision read/upload invoked for file {FileName} ({Length} bytes)", file.FileName, file.Length);

        await using Stream stream = file.OpenReadStream();
        VisionAnalysisResult result = await visionService.ReadImageStreamAsync(stream, cancellationToken);
        return Ok(result);
    }

    private static bool TryCreateImageUrl(string? value, out Uri uri)
    {
        if (!string.IsNullOrWhiteSpace(value) && Uri.TryCreate(value, UriKind.Absolute, out Uri? parsed))
        {
            uri = parsed;
            return true;
        }

        uri = default!;
        return false;
    }
}

public sealed class VisionUrlRequest
{
    public string ImageUrl { get; set; } = string.Empty;
}