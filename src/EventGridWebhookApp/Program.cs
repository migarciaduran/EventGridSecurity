using Azure.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Azure.Security.KeyVault.Secrets;
using System;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;

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

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Get the Application Insights connection string
string? appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"] ?? 
                                     Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

// Configure logging and Application Insights
builder.Logging.ClearProviders(); // Clear default providers first

if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    // Add Application Insights telemetry and logging
    builder.Services.AddApplicationInsightsTelemetry(options => 
    {
        options.ConnectionString = appInsightsConnectionString;
    });
    // The AddApplicationInsightsTelemetry extension automatically registers the Application Insights logger provider.
    // We can configure the minimum log level for Application Insights here if needed,
    // otherwise it defaults based on appsettings.json or environment variables.
    // Example: builder.Logging.AddFilter<ApplicationInsightsLoggerProvider>(null, LogLevel.Information); 
    
    // Add console logging as well, useful for seeing logs locally even when AppInsights is configured.
    builder.Logging.AddConsole(); 
}
else
{
    // Configure standard logging if Application Insights is not available
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();

    // Log a warning if in production and no connection string is available
    if (!builder.Environment.IsDevelopment())
    {
        // Use a temporary logger factory to log the warning since logging isn't fully built yet
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var logger = loggerFactory.CreateLogger<Program>();
        logger.LogWarning("No Application Insights connection string found. Telemetry will not be collected.");
    }
}

// Configure OpenTelemetry
if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(
                serviceName: builder.Environment.ApplicationName,
                serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddAzureMonitorTraceExporter(options => options.ConnectionString = appInsightsConnectionString));
}

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

app.Run();
