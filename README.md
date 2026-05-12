# AzureFoundryTest

**A hands-on learning platform for AI-102 certification and Azure AI services exploration.**

This ASP.NET Core Web API serves two purposes:
1. **Learning platform** — Testing ground for Azure AI concepts while preparing for AI-102
2. **SDK research lab** — Comparing the native `Azure.AI.OpenAI` SDK against the vendor-neutral `Microsoft.Extensions.AI` abstraction side-by-side

The codebase is intentionally kept small and focused so you can easily add services, test new patterns, and trace how everything connects. When you open this project, use this README to remember what you're currently testing and what concepts are in focus.

## Current Services & Controllers

| Service | Controller | Purpose | Learning Focus |
|---|---|---|---|
| **ChatAoaiService** | `AgentController` | Native Azure OpenAI SDK | Direct AOAI API patterns, response shapes |
| **ChatAoaiExtensionsService** | `AgentController` | M.E.AI abstraction layer | Vendor-neutral AI abstractions, middleware composition |
| **SentimentService** | `SentimentController` | Dual-provider sentiment analysis | Compare deterministic NLP vs LLM classification |
| **AzureDeploymentCatalog** | `AgentController` (`/api/agent/models`) | Live ARM discovery + fallback | Azure Resource Manager integration, identity & RBAC |
| **VisionService** | `VisionController` | Azure AI Vision image analysis and OCR | Analyze-by-URL, file upload, OCR, captions, tags |

## What This Demonstrates

- **SDK shape comparison.** Two endpoints hit the same deployment through different SDKs; each writes the raw SDK response object to a JSON file so the abstractions can be diffed line-by-line.
- **Middleware composition (M.E.AI only).** A custom `DelegatingChatClient` (tracing/logging) plus the framework's built-in `.UseOpenTelemetry()` layered on the same per-deployment client via DI. Demonstrates cross-cutting concerns that have no native AOAI equivalent.
- **Runtime model selection.** `IChatClientFactory` returns a cached-per-deployment `IChatClient` validated against an allowlist from live ARM discovery (with config-driven fallback). The native AOAI service deliberately doesn't get this — highlighting the abstraction gap.
- **Operational telemetry, three layers.** Framework instrumentation (ASP.NET Core, HttpClient, .NET runtime), M.E.AI's GenAI semantic-convention metrics, and app-specific meters for catalog/cache/quality signals — all bounded categorical, no tenant/user identifiers.
- **Data-sovereignty discipline encoded in code.** No `EnableSensitiveData`, no per-tenant metric labels, no secrets in committed config.

## Quick Start (Local Development)

### Prerequisites

- .NET 10 SDK
- An Azure OpenAI resource with at least one deployment
- An Azure identity (Visual Studio / Azure CLI / `az login`) that has:
  - Data-plane inference access on the resource (e.g. **Cognitive Services OpenAI User**)
  - ARM read access to list deployments (e.g. **Cognitive Services User**, **Reader**, or **Cognitive Services OpenAI Contributor**)

### Configuration

1. **Store secrets securely** (no API keys, Entra ID only):

```bash
# Run once per machine from the project directory:
dotnet user-secrets set "AzureOpenAI:Endpoint"        "https://<your-resource>.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:DeploymentName"  "<default-deployment-name>"
dotnet user-secrets set "AzureOpenAI:SubscriptionId"  "<subscription-guid>"
dotnet user-secrets set "AzureOpenAI:ResourceGroup"   "<resource-group-name>"
dotnet user-secrets set "AzureVision:Endpoint"        "https://<your-vision-resource>.cognitiveservices.azure.com/"
```

Values are stored securely per OS user and never committed to git.

2. **Run the app:**

```bash
dotnet run                                # http://localhost:5105
dotnet run --launch-profile https         # https://localhost:7263
```

Swagger UI is at `/swagger` in Development.

## API Endpoints

| Endpoint | Method | Purpose | Concept |
|---|---|---|---|
| `/health` | GET | Liveness probe (200 Healthy) | App health checks |
| `/api/agent/models` | GET | List Azure OpenAI deployments via ARM discovery | ARM SDK, identity, RBAC |
| `/api/agent/ask-agent-aoai` | POST | Query deployment using native `Azure.AI.OpenAI` SDK | Native AOAI SDK patterns |
| `/api/agent/ask-agent-ext` | POST | Query deployment using `Microsoft.Extensions.AI` abstraction | Vendor-neutral abstractions, middleware |
| `/api/sentiment/analyze` | POST | Analyze sentiment with both Azure AI Language and Azure OpenAI | NLP + LLM comparison patterns |
| `/api/vision/features` | GET | Show the Vision capabilities, limits, and supported endpoints | Vision setup guidance |
| `/api/vision/analyze/url` | POST | Analyze a public image URL | Caption, tags, objects, people |
| `/api/vision/analyze/upload` | POST | Analyze an uploaded image file | Caption, tags, objects, people |
| `/api/vision/read/url` | POST | Extract text from a public image URL | OCR |
| `/api/vision/read/upload` | POST | Extract text from an uploaded image file | OCR |

### Request format for chat endpoints:

```json
{ 
  "input": "What is the capital of Mexico?", 
  "model": "gpt-4o"
}
```

`model` is optional — omit it to use the configured default. The native AOAI endpoint logs and ignores `model` (intentional — highlights the abstraction difference).

### Request format for sentiment endpoint:

```json
{
  "text": "I love the speed, but setup was frustrating.",
  "model": "gpt-4o"
}
```

`model` is optional and only affects the Azure OpenAI sentiment path. If omitted, `AzureOpenAI:DeploymentName` is used.

## Project Structure

```
Controllers/
  ├── AgentController.cs         # Azure OpenAI chat endpoints
  └── SentimentController.cs     # Sentiment endpoint (Azure Language + Azure OpenAI)

Services/
  ├── ChatAoaiService.cs         # Native Azure.AI.OpenAI SDK wrapper
  ├── ChatAoaiExtensionsService.cs  # M.E.AI abstraction wrapper
  ├── ChatClientFactory.cs       # DI factory for chat clients
  ├── SentimentService.cs        # Dual-provider sentiment analysis service
  ├── AzureDeploymentCatalog.cs  # ARM discovery + fallback logic
  └── Interfaces/                # Service contracts

Models/
  ├── Chat/                      # Chat-related DTOs
  │   ├── AgentChatMessage.cs
  │   ├── AgentChatResponse.cs
  │   ├── AgentTextContent.cs
  │   ├── AgentFunctionCallContent.cs
  │   └── ... (other content types)
  └── Sentiment/
      └── SentimentResult.cs

Middleware/
  └── TracingChatClient.cs       # Custom DelegatingChatClient for observability

Diagnostics/
  └── AppMetrics.cs              # App-specific meter definitions
```

## AI-102 Learning Focus

This project covers several key AI-102 topics:

- **Azure OpenAI integration** — Working with deployments, models, and API patterns
- **Azure AI Vision integration** — Testing image analysis, OCR, captions, and file upload patterns
- **Identity & security** — Entra ID authentication, RBAC, no-secrets patterns, `DefaultAzureCredential`
- **ARM SDK usage** — Querying Azure resources programmatically to discover deployments
- **Abstraction patterns** — Native SDK vs. vendor-neutral abstractions; understanding when/why each matters
- **Observability** — Structured logging, distributed tracing, semantic conventions, metrics
- **Error handling** — Fallback strategies, resilience patterns, graceful degradation

As you add new services (sentiment analysis, embeddings, etc.), each will demonstrate a different Azure AI pattern.

## Observability

The app emits three streams of telemetry, all to stdout via OTel console exporters (replace with OTLP for any real backend):

- **Traces** — span sources `AzureFoundryTest.Agent` (controller actions), `AzureFoundryTest.Chat` (custom `TracingChatClient`), and `Experimental.Microsoft.Extensions.AI` (the framework's `.UseOpenTelemetry()` middleware). Same call produces multiple spans linked by trace ID; they tag with the GenAI semantic conventions (`gen_ai.*`).
- **Metrics** (every 10s):
  - **Layer 1 — framework**: `http.server.*`, `http.client.*` (covers ARM listing *and* Azure OpenAI SDK calls — both use HttpClient), `process.runtime.dotnet.*`.
  - **Layer 2 — M.E.AI built-in**: `gen_ai.client.operation.duration`, `gen_ai.client.token.usage`.
  - **Layer 3 — app-specific** (meter `AzureFoundryTest.App`): `agent.catalog.refresh{source}` (synthetic ARM-health signal — `source=config` rate spike means ARM listing is broken), `agent.catalog.lookup{result}` (`hit`/`coalesced`/`refreshed` — the value of double-checked locking made measurable), `agent.finish_reason{reason}` (quality/safety; `ContentFilter` rate is compliance-relevant), `agent.client_factory.size` (gauge).
- **Captured SDK responses** — every chat call writes the raw SDK response object as pretty-printed JSON to `responses/<ServiceClassName>.json` (overwritten per request, gitignored). This is the primary artifact for SDK-shape comparison: `ChatAoaiService` produces the OpenAI-wire JSON via `ModelReaderWriter.Write`, `ChatAoaiExtensionsService` produces the M.E.AI POCO shape via `System.Text.Json`.

In Visual Studio, console-exporter output lands in the **"ASP.NET Core Web Server"** output window; `ILogger` lines (controller and middleware) land in the **"Debug"** output window.

## Security Posture (AI-102 Concepts)

This codebase demonstrates secure-by-design patterns for AI applications:

- **No secrets committed.** API keys not supported — only Entra ID via `DefaultAzureCredential` (covers: managed identities, Visual Studio auth, Azure CLI, workload identity in containers/AKS)
- **No infrastructure IDs committed.** Subscription GUIDs, resource group names, account names, and URLs live in user-secrets or environment variables
- **No sensitive telemetry.** `EnableSensitiveData` left at `false` — prompts and completions never attached to spans or metrics
- **Bounded metrics dimensions.** No tenant/user/content labels on central metrics. Per-tenant accounting belongs in a separate audit pipeline
- **Gitignored artifacts:** `responses/`, `bin/`, `obj/`, local notes

**Learning takeaway:** This is how production AI apps protect customer data while maintaining full observability for operations.

## Artifacts & Debugging

- **SDK response dumps** — `responses/<ServiceName>.json` contains raw API responses for inspection
- **Console traces** — Run in Visual Studio's "ASP.NET Core Web Server" output window to see OTel spans in real time
- **Metrics** — Every 10 seconds, printed to console with `gen_ai.*` semantic conventions
- **Swagger** — Hit `/swagger` in Development to try endpoints directly

## Next Steps: Expanding the Project

As you continue learning for AI-102, consider adding these patterns:

- [ ] **Azure Cognitive Search** — Implement semantic search + vector search (AI Search service)
- [ ] **Embeddings service** — Compare native AOAI embeddings SDK vs. M.E.AI
- [ ] **Streaming responses** — Implement server-sent events (SSE) for streaming chat completions
- [ ] **Prompt engineering** — Add system prompts, temperature control, token budgeting
- [ ] **Function calling** — Demonstrate structured tool use and multi-turn conversations
- [x] **Sentiment analysis completion** — `SentimentService` now uses Azure OpenAI and Azure AI Language side-by-side
- [ ] **Deployment** — Containerize and deploy to Azure Container Apps or AKS
- [ ] **Content filtering** — Explore Azure OpenAI's content filtering and safety layers
- [ ] **Monitoring & alerts** — Hook up to Azure Monitor, Application Insights, or Datadog

Each addition is a learning opportunity. When you add a new feature, update this README with what concept it demonstrates.
