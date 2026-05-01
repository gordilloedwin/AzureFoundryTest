using System.Text.Json.Serialization;

namespace AzureFoundryTest.Models.Chat;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentChatFinishReason
{
    Stop,
    Length,
    ContentFilter,
    ToolCalls,
}
