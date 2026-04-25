using AzureFoundryTest.Swagger;
using AzureFoundryTest.Services;
using AzureFoundryTest.Services.Interfaces;
using AzureFoundryTest.Middleware;
using AzureFoundryTest.Controllers;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
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
