namespace AzureFoundryTest.Models.Chat;

/// <summary>In-memory binary content (image/audio/video bytes). For remote URLs use <see cref="AgentUriContent"/>.</summary>
public sealed class AgentDataContent : AgentAIContent
{
    public byte[]? Data { get; init; }

    public string? MediaType { get; init; }

    public string? Name { get; init; }
}
