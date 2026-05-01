namespace AzureFoundryTest.Models.Chat;

public sealed class AgentChatMessage
{
    public AgentChatRole Role { get; init; }

    public string? AuthorName { get; init; }

    public IList<AgentAIContent> Contents { get; init; } = [];

    public string? MessageId { get; init; }

    public IReadOnlyDictionary<string, object?>? AdditionalProperties { get; init; }

    /// <summary>Concatenation of every <see cref="AgentTextContent"/> in this message — agent-owned equivalent of <c>ChatMessage.Text</c>.</summary>
    public string Text => string.Concat(Contents.OfType<AgentTextContent>().Select(c => c.Text));
}
