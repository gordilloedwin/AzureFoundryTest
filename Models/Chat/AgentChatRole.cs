using System.Text.Json.Serialization;

namespace AzureFoundryTest.Models.Chat;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentChatRole
{
    System,
    User,
    Assistant,
    Tool,
}
