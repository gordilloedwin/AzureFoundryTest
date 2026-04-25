using System.Diagnostics.Metrics;

namespace AzureFoundryTest.Diagnostics;

// Static meter + instruments for app-specific operational metrics.
// Mirrors the pattern used for ActivitySource elsewhere — one well-known name,
// subscribed to by the OTel pipeline in Program.cs via metrics.AddMeter(AppMetrics.MeterName).
//
// All dimensions are bounded categorical values — no tenant IDs, no user IDs,
// no per-request identifiers. Safe to ship to a central collector under data-sovereignty rules.
public static class AppMetrics
{
	public const string MeterName = "AzureFoundryTest.App";
	public static readonly Meter Meter = new(MeterName);

	// Counter — increments on every catalog refresh attempt that actually contacts a source.
	// dimension: source = "azure" | "config"
	// Spike in source=config => ARM listing is broken, fallback path active.
	public static readonly Counter<long> CatalogRefresh = Meter.CreateCounter<long>(
		"agent.catalog.refresh",
		description: "Catalog refresh attempts, labeled by data source.");

	// Counter — increments on every ListAsync() call.
	// dimension: result = "hit" | "coalesced" | "refreshed"
	// hit       = served from fresh cache (fast path, no lock taken)
	// coalesced = entered the gate but another thread had already refreshed (the value of double-checked locking)
	// refreshed = this caller did the actual refresh
	public static readonly Counter<long> CatalogLookup = Meter.CreateCounter<long>(
		"agent.catalog.lookup",
		description: "Catalog lookups by cache outcome (hit/coalesced/refreshed).");

	// Counter — increments on every chat completion observed by the tracing middleware.
	// dimension: reason = "Stop" | "Length" | "ContentFilter" | "ToolCalls" | "unknown"
	// Quality + safety signal. ContentFilter rate is compliance-relevant.
	public static readonly Counter<long> FinishReason = Meter.CreateCounter<long>(
		"agent.finish_reason",
		description: "Chat response finish reasons.");
}
