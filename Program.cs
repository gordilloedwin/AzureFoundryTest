using AzureFoundryTest.Swagger;
using AzureFoundryTest.Services;
using AzureFoundryTest.Services.Interfaces;
using AzureFoundryTest.Middleware;
using AzureFoundryTest.Controllers;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using OpenTelemetry;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// --- Microsoft.Extensions.AI pipeline ---------------------------------------
// The IChatClient registered here is the one ChatAoaiExtensionsService consumes.
// The fluent .Use(...) call composes middleware around the inner Azure OpenAI client.
// Swap in .UseOpenTelemetry() for the framework-provided equivalent of our
// TracingChatClient (emits the full GenAI semantic-convention span set).
builder.Services.AddChatClient(services =>
{
    IConfiguration configuration = services.GetRequiredService<IConfiguration>();
    string endpoint = configuration["AzureOpenAI:Endpoint"]
        ?? throw new InvalidOperationException("Configuration value 'AzureOpenAI:Endpoint' is required.");
    string deployment = configuration["AzureOpenAI:DeploymentName"]
        ?? throw new InvalidOperationException("Configuration value 'AzureOpenAI:DeploymentName' is required.");

    return new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
        .GetChatClient(deployment)
        .AsIChatClient();
})
.Use((inner, services) => ActivatorUtilities.CreateInstance<TracingChatClient>(services, inner));

builder.Services.AddKeyedScoped<IChatService, ChatAoaiService>("aoai");
builder.Services.AddKeyedScoped<IChatService, ChatAoaiExtensionsService>("ext");

// Subscribe an OTel tracer to the ActivitySources the app emits on,
// and export to the console so you can see spans per request in the app log.
// Note: ConsoleExporter writes to stdout. In Visual Studio, stdout goes to the
// "ASP.NET Core Web Server" output window (via the Output window dropdown), NOT
// the Debug window. For the Debug window, watch the ILogger lines emitted by
// TracingChatClient and AgentController instead.
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.SetSampler(new AlwaysOnSampler());
        tracing.AddSource(AgentController.ActivitySource.Name);
        tracing.AddSource(TracingChatClient.ActivitySource.Name);
        tracing.AddConsoleExporter();
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
