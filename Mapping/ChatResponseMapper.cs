using AzureFoundryTest.Models.Chat;
using Microsoft.Extensions.AI;
using Riok.Mapperly.Abstractions;

namespace AzureFoundryTest.Mapping;

/// <summary>
/// Compile-time generated mapper from Microsoft.Extensions.AI's <see cref="ChatResponse"/> tree
/// to the agent-owned tree under <c>AzureFoundryTest.Models.Chat</c>. Mapperly fills in the
/// partial method bodies at build time — open <c>obj/{config}/{tfm}/generated/Riok.Mapperly</c>
/// after a build to read the generated source.
/// </summary>
/// <remarks>
/// <para><c>RequiredMappingStrategy.Target</c> means: every property on the target (Agent*)
/// type must be mappable. Source-only properties (<c>RawRepresentation</c>, computed <c>Text</c>,
/// any future SDK additions) are silently dropped — that's deliberate, since "full control" means
/// we decide what crosses the boundary, not the SDK.</para>
/// <para>The polymorphic <see cref="MapContent"/> method declares only the 8 derived types a
/// chat-completion response actually emits. If the SDK ever returns one we didn't list (hosted
/// tools, MCP content, etc.), Mapperly throws — fail loudly is the right default for a security-
/// minded project.</para>
/// </remarks>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class ChatResponseMapper
{
    /// <summary>Top-level mapping. <paramref name="deploymentUsed"/> binds to <see cref="AgentChatResponse.DeploymentUsed"/> by name.</summary>
    public static partial AgentChatResponse ToAgent(this ChatResponse source, string? deploymentUsed);

    private static partial AgentChatMessage MapMessage(ChatMessage source);

    private static partial AgentUsageDetails MapUsage(UsageDetails source);

    [MapDerivedType<TextContent, AgentTextContent>]
    [MapDerivedType<TextReasoningContent, AgentTextReasoningContent>]
    [MapDerivedType<DataContent, AgentDataContent>]
    [MapDerivedType<UriContent, AgentUriContent>]
    [MapDerivedType<FunctionCallContent, AgentFunctionCallContent>]
    [MapDerivedType<FunctionResultContent, AgentFunctionResultContent>]
    [MapDerivedType<UsageContent, AgentUsageContent>]
    [MapDerivedType<ErrorContent, AgentErrorContent>]
    private static partial AgentAIContent MapContent(AIContent source);

    // ── User-defined mappings (Mapperly auto-discovers by signature) ──────────

    // Compare by static instance, not by Value string — robust to any internal SDK rename.
    private static AgentChatRole MapRole(ChatRole role)
    {
        if (role == ChatRole.System) return AgentChatRole.System;
        if (role == ChatRole.User) return AgentChatRole.User;
        if (role == ChatRole.Assistant) return AgentChatRole.Assistant;
        if (role == ChatRole.Tool) return AgentChatRole.Tool;
        throw new ArgumentOutOfRangeException(nameof(role), $"Unknown chat role: '{role.Value}'");
    }

    private static AgentChatFinishReason? MapFinishReason(ChatFinishReason? reason)
    {
        if (reason is null) return null;
        ChatFinishReason r = reason.Value;
        if (r == ChatFinishReason.Stop) return AgentChatFinishReason.Stop;
        if (r == ChatFinishReason.Length) return AgentChatFinishReason.Length;
        if (r == ChatFinishReason.ContentFilter) return AgentChatFinishReason.ContentFilter;
        if (r == ChatFinishReason.ToolCalls) return AgentChatFinishReason.ToolCalls;
        return null;
    }

    private static byte[]? MapBytes(ReadOnlyMemory<byte> data) =>
        data.IsEmpty ? null : data.ToArray();

    private static IReadOnlyDictionary<string, object?>? MapAdditionalProperties(AdditionalPropertiesDictionary? source) =>
        source is null ? null : new Dictionary<string, object?>(source);

    private static IReadOnlyDictionary<string, long>? MapAdditionalCounts(AdditionalPropertiesDictionary<long>? source) =>
        source is null ? null : new Dictionary<string, long>(source);

    private static IReadOnlyDictionary<string, object?>? MapArguments(IDictionary<string, object?>? source) =>
        source is null ? null : new Dictionary<string, object?>(source);
}
