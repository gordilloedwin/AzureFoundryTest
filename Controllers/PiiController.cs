using AzureFoundryTest.Models.Pii;
using AzureFoundryTest.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AzureFoundryTest.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PiiController(ILogger<PiiController> logger) : ControllerBase
{
    [HttpPost("analyze")]
    public async Task<ActionResult<PiiAnalysisResult>> Analyze(
        [FromServices] IPiiService piiService,
        [FromBody] PiiRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Text))
        {
            return BadRequest("Text cannot be empty.");
        }

        logger.LogInformation("[controller] pii analysis invoked for text of length {Length}", request.Text.Length);

        PiiAnalysisResult result = await piiService.AnalyzeAsync(request.Text, request.Language, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}

public sealed class PiiRequest
{
    public string Text { get; set; } = string.Empty;

    // Optional BCP-47 language code, e.g. "en" or "es".
    public string? Language { get; set; }
}