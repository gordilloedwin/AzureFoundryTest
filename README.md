# AzureFoundryTest

ASP.NET Core Web API that compares two SDKs for calling the same Azure OpenAI deployment side-by-side: the native `Azure.AI.OpenAI` SDK and the vendor-neutral `Microsoft.Extensions.AI` abstraction. It is research scaffolding — small on purpose, built to produce runnable evidence (response JSON dumps, OpenTelemetry traces and metrics, Swagger UI) that informs architectural choices in other projects.

## What it demonstrates

- **SDK shape comparison.** Two endpoints hit the same deployment through different SDKs; each writes the raw SDK response object to a JSON file so the abstractions can be diffed line-by-line.
- **Middleware composition (M.E.AI only).** A custom `DelegatingChatClient` (tracing/logging) plus the framework's built-in `.UseOpenTelemetry()` layered on the same per-deployment client via DI. Demonstrates the kind of cross-cutting wiring that has no equivalent in the native AOAI SDK.
- **Runtime model selection.** `IChatClientFactory` returns a cached-per-deployment `IChatClient` validated against an allowlist sourced from a live ARM deployment listing (with config-driven fallback). The native AOAI service deliberately doesn't get this capability — the gap is the point.
- **Operational telemetry, three layers.** Framework instrumentation (ASP.NET Core, HttpClient, .NET runtime), M.E.AI's GenAI semantic-convention metrics, and a small app-specific meter for catalog/cache/quality signals — all dimensions bounded categorical, no tenant or user identifiers.
- **Data-sovereignty discipline encoded in code.** No `EnableSensitiveData`, no per-tenant labels on central metrics, no secrets in committed config.

## Prerequisites

- .NET 10 SDK
- An Azure OpenAI resource with at least one deployment
- An Azure identity (Visual Studio / Azure CLI / `az login`) that has:
  - Data-plane inference access on the resource (e.g. **Cognitive Services OpenAI User**)
  - ARM read access to list deployments (e.g. **Cognitive Services User**, **Reader**, or **Cognitive Services OpenAI Contributor**)

There are no API keys anywhere in the codebase. Authentication flows entirely through `DefaultAzureCredential`.

## Configuration

The committed `appsettings.json` contains only the *shape* of required config — all values are intentionally blank. Supply real values out-of-band.

### Local development: `dotnet user-secrets`

```bash
# Run once per machine from the project directory:
dotnet user-secrets set "AzureOpenAI:Endpoint"        "https://<your-resource>.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:DeploymentName"  "<default-deployment-name>"
dotnet user-secrets set "AzureOpenAI:SubscriptionId"  "<subscription-guid>"
dotnet user-secrets set "AzureOpenAI:ResourceGroup"   "<resource-group-name>"
# AccountName auto-derives from the Endpoint hostname; set it only if the derivation is wrong.
```

Values are stored under your OS user profile (`%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json` on Windows) and are not tracked by git.

### Other environments: environment variables

ASP.NET Core reads `__` as the section separator:

```bash
AzureOpenAI__Endpoint=https://...
AzureOpenAI__DeploymentName=...
AzureOpenAI__SubscriptionId=...
AzureOpenAI__ResourceGroup=...
```

### Fallback allowlist

If the running identity can't list deployments via ARM, the app still works — the deployment catalog falls back to `AzureOpenAI:AllowedDeployments` (array) or, if that's empty, the single `AzureOpenAI:DeploymentName`.

## Running

```bash
dotnet run                                # http://localhost:5105
dotnet run --launch-profile https         # + https://localhost:7263
```

Swagger UI is mounted at `/swagger` in Development. Endpoints:

| Endpoint | Purpose |
|---|---|
| `GET /health` | Liveness probe (200 Healthy when the process is up). |
| `GET /api/agent/models` | Live ARM deployment discovery with config fallback. |
| `POST /api/agent/ask-agent-aoai` | Native `Azure.AI.OpenAI` path (static, startup-bound deployment). |
| `POST /api/agent/ask-agent-ext` | `Microsoft.Extensions.AI` path with full middleware pipeline + runtime `model` selection. |

Request body for the `ask-agent-*` endpoints:

```json
{ "input": "What is the capital of Mexico?", "model": "gpt-4o" }
```

`model` is optional — when omitted, the configured default deployment is used. The native endpoint logs and ignores `model` (deliberately — that's the demo).

## Observability

The app emits three streams of telemetry, all to stdout via OTel console exporters (replace with OTLP for any real backend):

- **Traces** — span sources `AzureFoundryTest.Agent` (controller actions), `AzureFoundryTest.Chat` (custom `TracingChatClient`), and `Experimental.Microsoft.Extensions.AI` (the framework's `.UseOpenTelemetry()` middleware). Same call produces multiple spans linked by trace ID; they tag with the GenAI semantic conventions (`gen_ai.*`).
- **Metrics** (every 10s):
  - **Layer 1 — framework**: `http.server.*`, `http.client.*` (covers ARM listing *and* Azure OpenAI SDK calls — both use HttpClient), `process.runtime.dotnet.*`.
  - **Layer 2 — M.E.AI built-in**: `gen_ai.client.operation.duration`, `gen_ai.client.token.usage`.
  - **Layer 3 — app-specific** (meter `AzureFoundryTest.App`): `agent.catalog.refresh{source}` (synthetic ARM-health signal — `source=config` rate spike means ARM listing is broken), `agent.catalog.lookup{result}` (`hit`/`coalesced`/`refreshed` — the value of double-checked locking made measurable), `agent.finish_reason{reason}` (quality/safety; `ContentFilter` rate is compliance-relevant), `agent.client_factory.size` (gauge).
- **Captured SDK responses** — every chat call writes the raw SDK response object as pretty-printed JSON to `responses/<ServiceClassName>.json` (overwritten per request, gitignored). This is the primary artifact for SDK-shape comparison: `ChatAoaiService` produces the OpenAI-wire JSON via `ModelReaderWriter.Write`, `ChatAoaiExtensionsService` produces the M.E.AI POCO shape via `System.Text.Json`.

In Visual Studio, console-exporter output lands in the **"ASP.NET Core Web Server"** output window; `ILogger` lines (controller and middleware) land in the **"Debug"** output window.

## Security posture

- **No secrets committed.** API keys are not supported by design — only Entra-ID-backed credentials via `DefaultAzureCredential`.
- **No infrastructure identifiers committed.** Subscription GUIDs, resource group names, account names, and endpoint URLs live in user-secrets or environment variables.
- **No customer-content telemetry.** `OpenTelemetryChatClient.EnableSensitiveData` is left at its default (`false`); prompts and completions are never attached to spans.
- **No tenant or user labels on central metrics.** Bounded categorical dimensions only. Per-tenant token accounting (when needed) belongs in a separate, regional, audit-grade pipeline — not this one.
- **Gitignored:** `responses/`, `CLAUDE.md`, `bin/`, `obj/`.
