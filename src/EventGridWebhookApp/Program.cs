using Azure.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Azure.Security.KeyVault.Secrets;
using System;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Azure.Monitor.OpenTelemetry.Exporter;

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

// Add Application Insights for logging
builder.Services.AddApplicationInsightsTelemetry();

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: builder.Environment.ApplicationName,
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddAzureMonitorTraceExporter(options => 
        {
            // Optionally configure options - connection string will be picked up from ApplicationInsights settings
            options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
        }));

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
