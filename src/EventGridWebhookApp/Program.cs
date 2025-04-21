using Azure.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Azure.Security.KeyVault.Secrets;
using System;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Configure Key Vault integration if not in development
if (!builder.Environment.IsDevelopment())
{
    // Get the Key Vault URI from configuration
    var keyVaultName = builder.Configuration["KeyVaultName"];
    if (!string.IsNullOrEmpty(keyVaultName))
    {
        var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
        
        // Use DefaultAzureCredential for RBAC-based access
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions 
        {
            ManagedIdentityClientId = builder.Configuration["ManagedIdentityClientId"],
            ExcludeSharedTokenCacheCredential = true
        });
        
        // Register SecretClient for dependency injection
        builder.Services.AddSingleton(new SecretClient(keyVaultUri, credential));
        
        // Add Azure Key Vault as a configuration provider
        builder.Configuration.AddAzureKeyVault(keyVaultUri, credential);
    }
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks(); // Add Health Check services

// Get the Application Insights connection string (still needed for exporters and SDK)
string? appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"] ??
                                     Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

// --- Simplified OpenTelemetry Configuration ---

// Define the ResourceBuilder for OpenTelemetry
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName: builder.Environment.ApplicationName,
                serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown");

// Configure OpenTelemetry Logging
builder.Logging.ClearProviders(); // Clear default providers
builder.Logging.AddOpenTelemetry(options =>
{
    options.SetResourceBuilder(resourceBuilder);
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
    options.ParseStateValues = true;

    // Add Console exporter for local visibility
    options.AddConsoleExporter();

    // Add Azure Monitor exporter unconditionally
    // If connectionString is null/empty, it should handle it gracefully (won't send)
    options.AddAzureMonitorLogExporter(opt => opt.ConnectionString = appInsightsConnectionString);
});

// Configure OpenTelemetry Tracing and Metrics
builder.Services.AddOpenTelemetry()
    // Configure the resource details directly here using ConfigureResource
    .ConfigureResource(rb => rb.AddService( 
                serviceName: builder.Environment.ApplicationName,
                serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        // Add Azure Monitor exporter unconditionally
        .AddAzureMonitorTraceExporter(options => options.ConnectionString = appInsightsConnectionString))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        // Add other relevant meters if needed, e.g., .AddMeter("MyCustomMeter")
        // Add Azure Monitor exporter unconditionally
        .AddAzureMonitorMetricExporter(options => options.ConnectionString = appInsightsConnectionString));

// Add Application Insights SDK for richer features (Live Metrics, Profiler, etc.)
// This complements the OTel exporters.
if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = appInsightsConnectionString;
    });
}
// --- End of Simplified Configuration ---

// Register EventValidationService
builder.Services.AddScoped<EventGridWebhookApp.Services.IEventValidationService, EventGridWebhookApp.Services.EventValidationService>();

// Add authentication and authorization
builder.Services.AddAuthentication()
    .AddJwtBearer(options => {
        // These will now be loaded from Key Vault in production
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.Audience = builder.Configuration["Authentication:Audience"];
        
        // Add additional JWT bearer options
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        };
    });

builder.Services.AddAuthorization(options => {
    options.AddPolicy("EventGridPolicy", policy => {
        policy.RequireAuthenticatedUser();
        // You can add additional claims requirements based on your EventGrid setup
    });
});

// Add rate limiting
builder.Services.AddRateLimiter(options => {
    options.AddFixedWindowLimiter("fixed", options => {
        options.PermitLimit = 100;
        options.Window = TimeSpan.FromMinutes(1);
    });
});

// Add CORS policy
builder.Services.AddCors(options => {
    options.AddPolicy("EventGridCorsPolicy", policy => {
        policy.WithOrigins("https://management.azure.com")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Add global exception handling middleware
app.UseMiddleware<EventGridWebhookApp.Services.ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Add security headers in production
    app.UseHsts();
}

app.UseHttpsRedirection();

// Use CORS
app.UseCors("EventGridCorsPolicy");

// Use authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Use rate limiting
app.UseRateLimiter();

// Map controllers
app.MapControllers();

// Map the health check endpoint
app.MapHealthChecks("/healthz");

app.Run();
