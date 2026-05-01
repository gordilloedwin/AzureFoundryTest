namespace AzureFoundryTest.Models.Chat;

public sealed class AgentErrorContent : AgentAIContent
{
    public string Message { get; init; } = string.Empty;

    public string? ErrorCode { get; init; }

    public string? Details { get; init; }
}
