using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using AzureFoundryTest.Services.Interfaces;

namespace AzureFoundryTest.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    [HttpPost("ask-agent-aoai")]
    public Task<ActionResult<string>> AskAgentAoai(
        [FromKeyedServices("aoai")] IChatService chatService,
        [FromBody] AskAgentRequest request,
        CancellationToken cancellationToken)
        => HandleAsync(chatService, request, cancellationToken);

    [HttpPost("ask-agent-ext")]
    public Task<ActionResult<string>> AskAgentExt(
        [FromKeyedServices("ext")] IChatService chatService,
        [FromBody] AskAgentRequest request,
        CancellationToken cancellationToken)
        => HandleAsync(chatService, request, cancellationToken);

    private async Task<ActionResult<string>> HandleAsync(
        IChatService chatService,
        AskAgentRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Input))
        {
            return BadRequest("Input cannot be empty.");
        }

        string response = await chatService.AskAgentAsync(request.Input, cancellationToken);

        return Ok(response);
    }
}

public class AskAgentRequest
{
    public string Input { get; set; } = string.Empty;
}
