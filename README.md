# AzureFoundryTest

ASP.NET Core Web API that compares two SDKs for calling the same Azure OpenAI deployment side-by-side: the native `Azure.AI.OpenAI` SDK and the vendor-neutral `Microsoft.Extensions.AI` abstraction. It is research scaffolding — small on purpose, built to produce runnable evidence (response JSON dumps, OpenTelemetry traces, Swagger UI) that informs architectural choices in other projects.

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

Swagger UI is mounted at `/swagger` in Development. The three endpoints are:

| Endpoint | Purpose |
|---|---|
| `GET  /api/agent/models` | Live ARM deployment discovery with config fallback |
| `POST /api/agent/ask-agent-aoai` | Native `Azure.AI.OpenAI` path (static deployment) |
| `POST /api/agent/ask-agent-ext` | `Microsoft.Extensions.AI` path with middleware pipeline + runtime model selection |

Each call writes the raw SDK response object as pretty-printed JSON to `responses/<ServiceClassName>.json` (overwritten per request, gitignored) — the primary artifact for SDK-shape comparison.

## Security posture

- No secrets committed. API keys are not supported by design — only Entra-ID-backed credentials.
- No infrastructure identifiers committed. Subscription GUIDs, resource group names, and endpoint URLs live in user-secrets or environment variables.
- `responses/` and `CLAUDE.md` are gitignored.
