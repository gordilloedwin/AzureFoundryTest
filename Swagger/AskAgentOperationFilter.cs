using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace AzureFoundryTest.Swagger;

public class AskAgentOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        string? summary = context.MethodInfo.Name switch
        {
            "AskAgentAoai" => "Ask the agent via Azure.AI.OpenAI (native SDK).",
            "AskAgentExt" => "Ask the agent via Microsoft.Extensions.AI (IChatClient abstraction).",
            _ => null
        };

        if (summary is null)
        {
            return;
        }

        operation.Summary = summary;
        operation.Description = "Accepts a text prompt and returns a string response from the agent.";

        if (operation.RequestBody?.Content is not null &&
            operation.RequestBody.Content.TryGetValue("application/json", out OpenApiMediaType? requestMediaType) &&
            requestMediaType is not null)
        {
            requestMediaType.Example = new JsonObject
            {
                ["input"] = "Summarize the benefits of async programming in C#."
            };
        }

        if (operation.Responses is not null &&
            operation.Responses.TryGetValue("200", out IOpenApiResponse? okResponse) &&
            okResponse?.Content is not null)
        {
            foreach (var mediaType in okResponse.Content.Values)
            {
                mediaType.Example = JsonValue.Create("Agent response: Async improves responsiveness and scalability.");
            }
        }

        if (operation.Responses is not null &&
            operation.Responses.TryGetValue("400", out IOpenApiResponse? badRequestResponse) &&
            badRequestResponse?.Content is not null)
        {
            foreach (var mediaType in badRequestResponse.Content.Values)
            {
                mediaType.Example = JsonValue.Create("Input cannot be empty.");
            }
        }
    }
}
