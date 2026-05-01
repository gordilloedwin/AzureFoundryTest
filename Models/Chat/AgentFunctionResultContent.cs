namespace AzureFoundryTest.Models.Chat;

/// <summary>The result of a function/tool invocation, paired with the originating <see cref="AgentFunctionCallContent"/> by <see cref="CallId"/>.</summary>
public sealed class AgentFunctionResultContent : AgentAIContent
{
    public string CallId { get; init; } = string.Empty;

    // Function results are inherently provider-shaped (string, number, structured JSON).
    // object? is the honest type — we don't pretend to have a fixed schema here.
    public object? Result { get; init; }
}
