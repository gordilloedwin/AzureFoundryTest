namespace AzureFoundryTest.Models.Chat;

/// <summary>
/// Application-owned response shape for agent endpoints. Composition over inheritance:
/// every nested type is agent-owned (<see cref="AgentChatMessage"/>, <see cref="AgentChatRole"/>,
/// <see cref="AgentChatFinishReason"/>, <see cref="AgentUsageDetails"/>, <see cref="AgentAIContent"/>
/// and its derived types). The Microsoft.Extensions.AI SDK is touched only at the boundary;
/// past that point this contract is ours.
/// </summary>
public sealed class AgentChatResponse
{
    public IList<AgentChatMessage> Messages { get; init; } = [];

    public string? ResponseId { get; init; }

    public string? ConversationId { get; init; }

    public string? ModelId { get; init; }

    public DateTimeOffset? CreatedAt { get; init; }

    public AgentChatFinishReason? FinishReason { get; init; }

    public AgentUsageDetails? Usage { get; init; }

    public IReadOnlyDictionary<string, object?>? AdditionalProperties { get; init; }

    public string? DeploymentUsed { get; init; }

    /// <summary>Concatenation of every <see cref="AgentTextContent"/> across all messages — agent-owned equivalent of <c>ChatResponse.Text</c>.</summary>
    public string Text => string.Concat(
        Messages.SelectMany(m => m.Contents).OfType<AgentTextContent>().Select(c => c.Text));
}
