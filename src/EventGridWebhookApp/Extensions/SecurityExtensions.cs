using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

namespace EventGridWebhookApp.Extensions;

public static class SecurityExtensions
{
    public static IServiceCollection AddSecurityServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add authentication
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme) // Set default scheme
            .AddJwtBearer(options => {
                options.Authority = configuration["Authentication:Authority"];
                options.Audience = configuration["Authentication:Audience"];
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true
                    // Consider adding ClockSkew if needed: ClockSkew = TimeSpan.Zero
                };
                // Add logging for troubleshooting authentication issues if needed
                // options.Events = new JwtBearerEvents { OnAuthenticationFailed = context => { ... } };
            });

        // Add authorization
        services.AddAuthorization(options => {
            options.AddPolicy("EventGridPolicy", policy => {
                policy.RequireAuthenticatedUser();
                // Add specific claim requirements if necessary, e.g.,
                // policy.RequireClaim("scope", "eventgrid.events.read");
            });
            // Add a default policy that requires authentication for all endpoints unless explicitly allowed
            // options.FallbackPolicy = new AuthorizationPolicyBuilder()
            //     .RequireAuthenticatedUser()
            //     .Build();
        });

        // Configure rate limiting
        services.AddRateLimiter(options => {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests; // Standard response code
            options.AddFixedWindowLimiter("fixed", opt => {
                opt.PermitLimit = 100;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst; // Process requests FIFO
                opt.QueueLimit = 50; // Limit the number of queued requests
            });
            // Consider adding more sophisticated limiters (Sliding Window, Token Bucket, Concurrency) if needed
            // options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(...) // Example global limiter
        });

        // Configure CORS
        services.AddCors(options =>
        {
            options.AddPolicy("EventGridCorsPolicy", policy =>
            {
                // Be specific with allowed origins in production
                // Consider loading allowed origins from configuration
                policy.WithOrigins("https://management.azure.com") // Keep this specific
                      .AllowAnyMethod() // Or specify methods: .WithMethods("GET", "POST", "OPTIONS")
                      .AllowAnyHeader() // Or specify headers: .WithHeaders("Content-Type", "Authorization")
                      .AllowCredentials();
                // Consider adding a more restrictive policy for general use if needed
            });
        });

        return services;
    }
}
