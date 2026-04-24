using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using AzureFoundryTest.Services.Interfaces;
using System.Diagnostics;

namespace AzureFoundryTest.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController(ILogger<AgentController> logger) : ControllerBase
{
    public static readonly ActivitySource ActivitySource = new("AzureFoundryTest.Agent");

    [HttpPost("ask-agent-aoai")]
    public async Task<ActionResult<string>> AskAgentAoai(
        [FromKeyedServices("aoai")] IChatService chatService,
        [FromBody] AskAgentRequest request,
        CancellationToken cancellationToken)
    {
        using Activity? activity = ActivitySource.StartActivity("ask-agent.aoai", ActivityKind.Server);
        activity?.SetTag("agent.sdk", "Azure.AI.OpenAI (native)");
        logger.LogInformation("[controller] ask-agent.aoai invoked — SDK=Azure.AI.OpenAI (native, no middleware pipeline)");

        return await HandleAsync(chatService, request, cancellationToken);
    }

    [HttpPost("ask-agent-ext")]
    public async Task<ActionResult<string>> AskAgentExt(
        [FromKeyedServices("ext")] IChatService chatService,
        [FromBody] AskAgentRequest request,
        CancellationToken cancellationToken)
    {
        using Activity? activity = ActivitySource.StartActivity("ask-agent.ext", ActivityKind.Server);
        activity?.SetTag("agent.sdk", "Microsoft.Extensions.AI");
        logger.LogInformation("[controller] ask-agent.ext invoked — SDK=Microsoft.Extensions.AI (TracingChatClient middleware will emit a child span)");

        return await HandleAsync(chatService, request, cancellationToken);
    }

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
