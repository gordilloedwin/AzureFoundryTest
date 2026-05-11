using AzureFoundryTest.Models.Sentiment;
using AzureFoundryTest.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AzureFoundryTest.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SentimentController(ILogger<SentimentController> logger) : ControllerBase
{
    [HttpPost("analyze")]
    public async Task<ActionResult<SentimentResult>> AnalyzeSentiment(
        [FromServices] ISentimentService sentimentService,
        [FromBody] SentimentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Text))
        {
            return BadRequest("Text cannot be empty.");
        }

        logger.LogInformation("[controller] sentiment analysis invoked for text of length {Length}", request.Text.Length);

        SentimentResult result = await sentimentService.AnalyzeAsync(request.Text, request.Model, cancellationToken);
        return Ok(result);
    }
}

public class SentimentRequest
{
    public string Text { get; set; } = string.Empty;

    // Optional Azure OpenAI deployment override for the AOAI sentiment path.
    public string? Model { get; set; }
}
