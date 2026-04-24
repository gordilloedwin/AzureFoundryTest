using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace AzureFoundryTest.Swagger;

public class AskAgentOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.Name != "AskAgent")
        {
            return;
        }

        operation.Summary = "Ask the agent a question.";
        operation.Description = "Accepts a text prompt and returns a string response from the agent.";

        if (operation.RequestBody?.Content is not null &&
            operation.RequestBody.Content.TryGetValue("application/json", out OpenApiMediaType? requestMediaType) &&
            requestMediaType is not null)
        {
            requestMediaType.Example = new JsonObject
            {
                ["input"] = "What is the capital of France?"
            };
        }

        if (operation.Responses is not null &&
            operation.Responses.TryGetValue("200", out IOpenApiResponse? okResponse) &&
            okResponse?.Content is not null)
        {
            foreach (var mediaType in okResponse.Content.Values)
            {
                mediaType.Example = JsonValue.Create("Agent response: The capital of France is Paris.");
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
