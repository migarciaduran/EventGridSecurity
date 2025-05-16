using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System; // Added for ArgumentNullException
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace EventGridWebhookApp.Extensions;

public static class SecurityExtensions
{
    public static IServiceCollection AddSecurityServices(this IServiceCollection services, IConfiguration configuration)
    {
        string? authority = configuration["Authentication:Authority"];
        string? audience = configuration["Authentication:Audience"];

        if (string.IsNullOrEmpty(authority))
        {
            throw new ArgumentNullException(nameof(authority), "Authentication:Authority configuration is missing or empty.");
        }
        if (string.IsNullOrEmpty(audience))
        {
            throw new ArgumentNullException(nameof(audience), "Authentication:Audience configuration is missing or empty.");
        }

        // Add authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.Authority = authority; // Use variable

            // Use strict token validation parameters
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true, // This handles audience validation
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.FromMinutes(5),

                ValidAudience = audience, // Use variable

                ValidIssuers = new[] {
                    authority.TrimEnd('/'), // Use variable
                    // Add any additional trusted issuers from your 3PP provider if needed
                }
            };

            // Add event handlers for monitoring and diagnostic purposes
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger<JwtBearerEvents>();

                    // Log detailed error if available
                    logger.LogWarning(context.Exception, "Authentication failed: {Message}", context.Exception?.Message ?? "No exception details");
                    context.Response.StatusCode = 401; // Ensure status code is set
                    return Task.CompletedTask;
                },
                OnForbidden = context =>
                {
                    var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger<JwtBearerEvents>();

                    logger.LogWarning("Access forbidden for user: {User}", context.Principal?.Identity?.Name ?? "unknown");
                    // No need to set status code here, middleware handles it
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    // The core audience validation is handled by TokenValidationParameters.ValidateAudience = true
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                    var token = context.SecurityToken as JwtSecurityToken;

                    // Extract the appId claim - this should be Event Grid's application ID
                    var appId = context.Principal?.FindFirstValue("appid");
                    var isEventGridAppId = appId == "4962773b-9cdb-44cf-a8bf-237846a00ab7";

                    if (isEventGridAppId)
                    {
                        logger.LogInformation("Token successfully validated from Azure EventGrid appId with audience {Audience}",
                            token?.Audiences?.FirstOrDefault() ?? "N/A");
                    }
                    else
                    {
                        // Log a warning if the token is not from Event Grid but still valid
                        logger.LogWarning("Token validated but from unexpected application: AppId={AppId}, Subject={Subject}",
                            appId, token?.Subject);
                    }

                    return Task.CompletedTask;
                }
            };

            // Save token in authentication properties for easy access
            options.SaveToken = true;
        });        // Add authorization
        services.AddAuthorization(options =>
        {
            options.AddPolicy("EventGridPolicy", policy =>
            {
                policy.RequireAuthenticatedUser();

                // Require specific audience claim (already validated by JWT middleware, but good for policy clarity)
                policy.RequireClaim("aud", audience); // Use variable

                // Add a check for the Azure EventGrid App ID
                // Event Grid's app ID in Microsoft's tenant is always this value
                policy.RequireClaim("appid", "4962773b-9cdb-44cf-a8bf-237846a00ab7"); // Azure Event Grid appId

                

                // Optional: If your 3PP provider includes specific scopes/permissions
                // Uncomment to require specific scope/permission:
                // policy.RequireClaim("scope", "eventgrid.events.write", "eventgrid.events.read");

                // Optional: Add role-based validation if your 3PP provider supports roles
                // policy.RequireRole("EventGridPublisher");
            });
        });

        // Configure rate limiting
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests; // Standard response code
            options.AddFixedWindowLimiter("fixed", opt =>
            {
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
                // --- Improvement: More specific CORS policy ---
                // Be specific with allowed origins in production
                // Consider loading allowed origins from configuration
                policy.WithOrigins("https://management.azure.com") // Keep this specific for Azure Portal Event Grid interactions
                      .WithMethods("POST", "OPTIONS") // Event Grid typically uses POST for delivery, OPTIONS for preflight
                      .WithHeaders("Content-Type", "Authorization", "aeg-event-type") // Common headers for webhooks + Event Grid specific header
                      .AllowCredentials(); // If needed, otherwise remove
                // --- End Improvement ---
                // Consider adding a more restrictive policy for general use if needed
            });
        });

        return services;
    }
}
