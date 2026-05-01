namespace AzureFoundryTest.Models.Chat;

/// <summary>Embeds usage details inline within a message's content list (some providers report usage this way mid-stream).</summary>
public sealed class AgentUsageContent : AgentAIContent
{
    public AgentUsageDetails Details { get; init; } = new();
}
