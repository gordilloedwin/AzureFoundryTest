namespace AzureFoundryTest.Models.Chat;

public sealed class AgentUsageDetails
{
    public long? InputTokenCount { get; init; }

    public long? OutputTokenCount { get; init; }

    public long? TotalTokenCount { get; init; }

    public IReadOnlyDictionary<string, long>? AdditionalCounts { get; init; }
}
