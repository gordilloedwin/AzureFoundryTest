using Microsoft.AspNetCore.Mvc;
using AzureFoundryTest.Services.Interfaces;

namespace AzureFoundryTest.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController(IChatService chatService) : ControllerBase
{
    [HttpPost("ask-agent")]
    public async Task<ActionResult<string>> AskAgent([FromBody] AskAgentRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Input))
        {
            return BadRequest("Input cannot be empty.");
        }

        string response = await chatService.AskAgentAsync(request.Input);

        return Ok(response);
    }
}

public class AskAgentRequest
{
    public string Input { get; set; } = string.Empty;
}
