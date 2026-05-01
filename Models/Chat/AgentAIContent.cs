using System.Text.Json.Serialization;

namespace AzureFoundryTest.Models.Chat;

/// <summary>
/// Application-owned base for content items inside <see cref="AgentChatMessage"/>.
/// Polymorphic discriminator mirrors the Microsoft.Extensions.AI SDK's <c>$type</c> tags
/// so JSON dumps line up readably even though the runtime types are different.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(AgentTextContent), "text")]
[JsonDerivedType(typeof(AgentTextReasoningContent), "reasoning")]
[JsonDerivedType(typeof(AgentDataContent), "data")]
[JsonDerivedType(typeof(AgentUriContent), "uri")]
[JsonDerivedType(typeof(AgentFunctionCallContent), "functionCall")]
[JsonDerivedType(typeof(AgentFunctionResultContent), "functionResult")]
[JsonDerivedType(typeof(AgentUsageContent), "usage")]
[JsonDerivedType(typeof(AgentErrorContent), "error")]
public abstract class AgentAIContent
{
    public IReadOnlyDictionary<string, object?>? AdditionalProperties { get; init; }
}
