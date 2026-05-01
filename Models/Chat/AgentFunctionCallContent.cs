namespace AzureFoundryTest.Models.Chat;

/// <summary>A request from the model to invoke a tool/function. Pair with <see cref="AgentFunctionResultContent"/> by <see cref="CallId"/>.</summary>
public sealed class AgentFunctionCallContent : AgentAIContent
{
    public string CallId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, object?>? Arguments { get; init; }
}
