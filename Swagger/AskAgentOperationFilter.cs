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
            "GetModels" => "List deployments available to the running identity (live from Azure with config fallback).",
            _ => null
        };

        if (summary is null)
        {
            return;
        }

        operation.Summary = summary;

        if (context.MethodInfo.Name == "GetModels")
        {
            operation.Description = "Hit this first to discover which `model` values are accepted by the ask-agent-* endpoints.";
            return;
        }

        operation.Description = "Accepts a text prompt and returns a string response from the agent. " +
            "`model` is optional; when omitted, the service uses its configured default deployment. " +
            "Note: the native AOAI endpoint ignores `model` and only honors the startup-bound deployment.";

        if (operation.RequestBody?.Content is not null &&
            operation.RequestBody.Content.TryGetValue("application/json", out OpenApiMediaType? requestMediaType) &&
            requestMediaType is not null)
        {
            requestMediaType.Example = new JsonObject
            {
                ["input"] = "What is the capital of Mexico?",
                ["model"] = "gpt-4o"
            };
        }

        if (operation.Responses is not null &&
            operation.Responses.TryGetValue("200", out IOpenApiResponse? okResponse) &&
            okResponse?.Content is not null)
        {
            foreach (var mediaType in okResponse.Content.Values)
            {
                mediaType.Example = JsonValue.Create("Agent response: The capital of Mexico is Mexico City.");
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
