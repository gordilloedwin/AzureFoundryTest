namespace AzureFoundryTest.Models.Chat;

/// <summary>A URL pointing at hosted content (image/audio/video). For inline bytes use <see cref="AgentDataContent"/>.</summary>
public sealed class AgentUriContent : AgentAIContent
{
    public Uri? Uri { get; init; }

    public string? MediaType { get; init; }
}
