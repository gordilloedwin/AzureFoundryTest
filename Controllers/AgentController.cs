using Microsoft.AspNetCore.Mvc;

namespace AzureFoundryTest.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    [HttpPost("ask-agent")]
    public async Task<ActionResult<string>> AskAgent([FromBody] AskAgentRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Input))
        {
            return BadRequest("Input cannot be empty.");
        }

        await Task.CompletedTask;

        return Ok($"Agent response: {request.Input}");
    }
}

public class AskAgentRequest
{
    public string Input { get; set; } = string.Empty;
}
