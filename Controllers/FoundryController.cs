using AzureFoundryTest.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AzureFoundryTest.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoundryController(ILogger<FoundryController> logger) : ControllerBase
{
    [HttpPost("ask")]
    public async Task<ActionResult<string>> Ask(
        [FromServices] IFoundryService foundryService,
        [FromBody] FoundryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Input))
        {
            return BadRequest("Input cannot be empty.");
        }

        logger.LogInformation("[controller] foundry ask invoked — model={Model}", request.Model ?? "<default>");

        try
        {
            string response = await foundryService.AskAsync(request.Input, request.Model, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[controller] foundry ask failed");
            return StatusCode(500, ex.Message);
        }
    }
}

public sealed class FoundryRequest
{
    public string Input { get; set; } = string.Empty;

    // Optional — overrides the default model configured in AzureFoundry:Model.
    public string? Model { get; set; }
}
