using AzureFoundryTest.Swagger;
using AzureFoundryTest.Services;
using AzureFoundryTest.Services.Interfaces;
using AzureFoundryTest.Middleware;
using AzureFoundryTest.Controllers;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
builder.Services.AddSingleton<AzureOpenAIClient>(sp =>
{
    IConfiguration config = sp.GetRequiredService<IConfiguration>();
    TokenCredential credential = sp.GetRequiredService<TokenCredential>();
    string endpoint = config["AzureOpenAI:Endpoint"]
        ?? throw new InvalidOperationException("Configuration value 'AzureOpenAI:Endpoint' is required.");
    return new AzureOpenAIClient(new Uri(endpoint), credential);
});

builder.Services.AddSingleton<IDeploymentCatalog, AzureDeploymentCatalog>();
builder.Services.AddSingleton<IChatClientFactory, ChatClientFactory>();
builder.Services.AddKeyedScoped<IChatService, ChatAoaiService>("aoai");
builder.Services.AddKeyedScoped<IChatService, ChatAoaiExtensionsService>("ext");

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.SetSampler(new AlwaysOnSampler());
        tracing.AddSource(AgentController.ActivitySource.Name);
        tracing.AddSource(TracingChatClient.ActivitySource.Name);
        // Layer 2 — M.E.AI's built-in .UseOpenTelemetry() middleware emits richer GenAI-semconv spans
        // alongside our custom TracingChatClient. Both fire on the same call; you can compare span output.
        tracing.AddSource("Experimental.Microsoft.Extensions.AI");
        tracing.AddConsoleExporter();
    })
    .WithMetrics(metrics =>
    {
        // Layer 1 — "is the system ok?" telemetry, all from framework instrumentation.
        // Inbound HTTP: http.server.request.duration (p50/p95/p99 by route + status), http.server.active_requests.
        // Outbound HTTP (covers ARM listing AND Azure OpenAI SDK calls — both use HttpClient):
        // http.client.request.duration, http.client.active_requests, connection metrics.
        // .NET runtime: GC stats, threadpool queue length/starvation, exceptions/sec, working set.
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddHttpClientInstrumentation();
        metrics.AddRuntimeInstrumentation();

        // Layer 2 — GenAI semantic-convention metrics from M.E.AI's .UseOpenTelemetry() middleware.
        // gen_ai.client.operation.duration (histogram by model), gen_ai.client.token.usage
        // (histogram by direction=input|output). Same emitter, same conventions, no custom code.
        metrics.AddMeter("Experimental.Microsoft.Extensions.AI");

        // Console export every 10s — keeps the demo console readable instead of flooding it.
        metrics.AddConsoleExporter((_, readerOptions) =>
        {
            readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 10_000;
        });
    });

builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<AskAgentOperationFilter>();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
