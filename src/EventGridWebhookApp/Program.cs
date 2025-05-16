using EventGridWebhookApp.Extensions; // Add this using statement
using EventGridWebhookApp.Services; // Keep for IEventValidationService registration

var builder = WebApplication.CreateBuilder(args);

builder.AddKeyVaultConfiguration(); // Use extension method

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

builder.Services.AddScoped<IEventValidationService, EventValidationService>();

builder.AddObservability();

builder.Services.AddSecurityServices(builder.Configuration); 

var app = builder.Build();

app.ConfigurePipeline(); // Use extension method
app.Run();

public partial class Program { }
