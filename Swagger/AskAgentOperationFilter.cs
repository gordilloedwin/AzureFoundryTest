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
            "AnalyzeSentiment" => "Analyze the sentiment of a text using Azure AI Language.",
            "GetFeatures" => "Show the Azure AI Vision capabilities exposed by this demo API.",
            "AnalyzeUrl" => "Analyze a public image URL with Azure AI Vision.",
            "AnalyzeUpload" => "Analyze an uploaded image file with Azure AI Vision.",
            "ReadUrl" => "Extract OCR text from a public image URL with Azure AI Vision.",
            "ReadUpload" => "Extract OCR text from an uploaded image file with Azure AI Vision.",
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

        if (context.MethodInfo.Name == "GetFeatures")
        {
            operation.Description = "Returns the supported Vision routes, common format limits, and the feature set this demo is wired to explore first.";

            if (operation.Responses is not null &&
                operation.Responses.TryGetValue("200", out IOpenApiResponse? okFeaturesResponse) &&
                okFeaturesResponse?.Content is not null)
            {
                foreach (var mediaType in okFeaturesResponse.Content.Values)
                {
                    mediaType.Example = new JsonObject
                    {
                        ["resourceNotes"] = "Use a Computer Vision or Azure AI Vision resource with DefaultAzureCredential and Cognitive Services User access.",
                        ["supportedEndpoints"] = new JsonArray(
                            "/api/vision/analyze/url",
                            "/api/vision/analyze/upload",
                            "/api/vision/read/url",
                            "/api/vision/read/upload"),
                        ["supportedFormats"] = new JsonArray("JPEG", "PNG", "GIF", "BMP", "WEBP", "ICO", "TIFF", "MPO")
                    };
                }
            }

            return;
        }

        if (context.MethodInfo.Name == "AnalyzeSentiment")
        {
            operation.Description = "Submits text to Azure AI Language and returns the detected sentiment " +
                "(Positive, Negative, Neutral, or Mixed) with confidence scores for each category.";

            if (operation.RequestBody?.Content is not null &&
                operation.RequestBody.Content.TryGetValue("application/json", out OpenApiMediaType? sentimentRequestMediaType) &&
                sentimentRequestMediaType is not null)
            {
                sentimentRequestMediaType.Example = new JsonObject
                {
                    ["text"] = "I absolutely loved the new product — it exceeded all my expectations!"
                };
            }

            return;
        }

        if (context.MethodInfo.Name is "AnalyzeUrl" or "ReadUrl")
        {
            operation.Description = context.MethodInfo.Name == "AnalyzeUrl"
                ? "Pass a publicly accessible image URL. The service returns a caption plus the raw Vision JSON payload for inspection in Swagger."
                : "Pass a publicly accessible image URL. The service returns OCR text lines plus the raw Vision JSON payload for inspection in Swagger.";

            if (operation.RequestBody?.Content is not null &&
                operation.RequestBody.Content.TryGetValue("application/json", out OpenApiMediaType? visionRequestMediaType) &&
                visionRequestMediaType is not null)
            {
                visionRequestMediaType.Example = new JsonObject
                {
                    ["imageUrl"] = context.MethodInfo.Name == "AnalyzeUrl"
                        ? "https://aka.ms/azsdk/image-analysis/sample.jpg"
                        : "https://learn.microsoft.com/azure/ai-services/computer-vision/media/quickstarts/presentation.png"
                };
            }

            if (operation.Responses is not null &&
                operation.Responses.TryGetValue("200", out IOpenApiResponse? okVisionResponse) &&
                okVisionResponse?.Content is not null)
            {
                foreach (var mediaType in okVisionResponse.Content.Values)
                {
                    mediaType.Example = context.MethodInfo.Name == "AnalyzeUrl"
                        ? CreateVisionAnalyzeExample()
                        : CreateVisionReadExample();
                }
            }

            return;
        }

        if (context.MethodInfo.Name is "AnalyzeUpload" or "ReadUpload")
        {
            operation.Description = context.MethodInfo.Name == "AnalyzeUpload"
                ? "Upload an image file as multipart/form-data using the `file` field. Optional query string: `genderNeutralCaption=true`."
                : "Upload an image file as multipart/form-data using the `file` field to extract OCR text lines.";

            if (operation.Responses is not null &&
                operation.Responses.TryGetValue("200", out IOpenApiResponse? okUploadResponse) &&
                okUploadResponse?.Content is not null)
            {
                foreach (var mediaType in okUploadResponse.Content.Values)
                {
                    mediaType.Example = context.MethodInfo.Name == "AnalyzeUpload"
                        ? CreateVisionAnalyzeExample()
                        : CreateVisionReadExample();
                }
            }

            if (operation.Responses is not null &&
                operation.Responses.TryGetValue("400", out IOpenApiResponse? badUploadResponse) &&
                badUploadResponse?.Content is not null)
            {
                foreach (var mediaType in badUploadResponse.Content.Values)
                {
                    mediaType.Example = JsonValue.Create("File cannot be empty.");
                }
            }

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

    private static JsonObject CreateVisionAnalyzeExample()
    {
        return new JsonObject
        {
            ["operation"] = "analyze-url",
            ["sourceKind"] = "url",
            ["source"] = "https://aka.ms/azsdk/image-analysis/sample.jpg",
            ["modelVersion"] = "latest",
            ["caption"] = "A group of people standing on a beach with surfboards.",
            ["captionConfidence"] = 0.91,
            ["textLines"] = new JsonArray(),
            ["rawJson"] = "{\n  \"caption\": { \"text\": \"A group of people standing on a beach with surfboards.\", \"confidence\": 0.91 }\n}"
        };
    }

    private static JsonObject CreateVisionReadExample()
    {
        return new JsonObject
        {
            ["operation"] = "read-url",
            ["sourceKind"] = "url",
            ["source"] = "https://learn.microsoft.com/azure/ai-services/computer-vision/media/quickstarts/presentation.png",
            ["modelVersion"] = "latest",
            ["caption"] = null,
            ["captionConfidence"] = null,
            ["textLines"] = new JsonArray(
                "Azure AI Vision",
                "Read API quickstart",
                "Sample slide content"),
            ["rawJson"] = "{\n  \"read\": { \"blocks\": [ { \"lines\": [ { \"text\": \"Azure AI Vision\" } ] } ] }\n}"
        };
    }
}
