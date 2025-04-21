using EventGridWebhookApp.Extensions; // Add this using statement
using EventGridWebhookApp.Services; // Keep for IEventValidationService registration

var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---
builder.AddKeyVaultConfiguration(); // Use extension method

// --- Service Registration ---

// Add framework services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// Add custom application services
builder.Services.AddScoped<IEventValidationService, EventValidationService>();

// Add observability (OpenTelemetry, Application Insights)
builder.AddObservability(); // Use extension method

// Add security services (AuthN, AuthZ, CORS, Rate Limiting)
builder.Services.AddSecurityServices(builder.Configuration); // Use extension method

// --- Build the App ---
var app = builder.Build();

// --- Configure Middleware Pipeline ---
app.ConfigurePipeline(); // Use extension method

// --- Run the App ---
app.Run();

// Make Program class public for tests or other references if needed
public partial class Program { }
