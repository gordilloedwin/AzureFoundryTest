using Microsoft.AspNetCore.Mvc;
using AzureFoundryTest.Services.Interfaces;
using System.Diagnostics;

namespace AzureFoundryTest.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController(ILogger<AgentController> logger) : ControllerBase
{
    public static readonly ActivitySource ActivitySource = new("AzureFoundryTest.Agent");

    [HttpGet("models")]
    public async Task<ActionResult<IReadOnlyList<DeploymentInfo>>> GetModels(
        [FromServices] IDeploymentCatalog catalog,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DeploymentInfo> deployments = await catalog.ListAsync(cancellationToken);
        return Ok(deployments);
    }

    [HttpPost("ask-agent-aoai")]
    public async Task<ActionResult<string>> AskAgentAoai(
        [FromKeyedServices("aoai")] IChatService chatService,
        [FromBody] AskAgentRequest request,
        CancellationToken cancellationToken)
    {
        using Activity? activity = ActivitySource.StartActivity("ask-agent.aoai", ActivityKind.Server);
        activity?.SetTag("agent.sdk", "Azure.AI.OpenAI (native)");
        activity?.SetTag("agent.requested_model", request?.Model);
        logger.LogInformation("[controller] ask-agent.aoai invoked — SDK=Azure.AI.OpenAI (native), requested model={Model}", request?.Model ?? "<default>");

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
        activity?.SetTag("agent.requested_model", request?.Model);
        logger.LogInformation("[controller] ask-agent.ext invoked — SDK=Microsoft.Extensions.AI, requested model={Model}", request?.Model ?? "<default>");

        return await HandleAsync(chatService, request, cancellationToken);
    }

    private async Task<ActionResult<string>> HandleAsync(
        IChatService chatService,
        AskAgentRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Input))
        {
            return BadRequest("Input cannot be empty.");
        }

        try
        {
            string response = await chatService.AskAgentAsync(request.Input, request.Model, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex) when (ex.ParamName == "deployment")
        {
            return BadRequest(ex.Message);
        }
    }
}

public class AskAgentRequest
{
    public string Input { get; set; } = string.Empty;

    // Optional — when null or empty, the service uses its configured default deployment.
    // Hit GET /api/agent/models to see which deployment names are accepted.
    public string? Model { get; set; }
}
