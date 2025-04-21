using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace EventGridWebhookApp.Extensions;

public static class SecurityExtensions
{
    public static IServiceCollection AddSecurityServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Get audience from configuration with null safety
        string? audienceValue = configuration["Authentication:Authority"];
        
        // Add authentication
        services.AddAuthentication(options => {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options => {
            // Configure authority (identity provider)
            options.Authority = configuration["Authentication:Authority"];
            options.Audience = configuration["Authentication:Audience"];
            
            // Use strict token validation parameters
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, 
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.FromMinutes(5), // Reasonable tolerance for clock differences
                
                // Ensure the audience matches exactly what's expected
                ValidAudience = configuration["Authentication:Audience"],
                
                // You can also specify valid issuers if you have multiple trusted identity providers
                ValidIssuers = new[] { 
                    configuration["Authentication:Authority"]?.TrimEnd('/') ?? string.Empty,
                    // Add any additional trusted issuers from your 3PP provider if needed
                }
            };
            
            // Add event handlers for monitoring and diagnostic purposes
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    // Fix: Get logger from services instead of using context.Logger
                    var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger<JwtBearerEvents>();
                    
                    context.Response.StatusCode = 401;
                    logger.LogWarning("Authentication failed: {Message}", context.Exception.Message);
                    return Task.CompletedTask;
                },
                OnForbidden = context =>
                {
                    // Fix: Get logger from services instead of using context.Logger
                    var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger<JwtBearerEvents>();
                    
                    logger.LogWarning("Access forbidden for user: {User}", context.Principal?.Identity?.Name ?? "unknown");
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                    var token = context.SecurityToken as JwtSecurityToken;
                    
                    // Additional audience validation with proper null handling
                    string? configAudience = configuration["Authentication:Audience"];
                    string? tokenAudience = token?.Audiences?.FirstOrDefault();

                    // Handle cases where configuration or token audience might be null
                    if (string.IsNullOrEmpty(configAudience))
                    {
                        logger.LogWarning("No audience configured in Authentication:Audience setting");
                        // Decide if this should be a validation failure
                        // context.Fail("No audience configured");
                        // return Task.CompletedTask;
                    }
                    else if (string.IsNullOrEmpty(tokenAudience))
                    {
                        logger.LogWarning("Token contains no audience claim");
                        context.Fail("Invalid token - no audience claim");
                        return Task.CompletedTask;
                    }
                    else if (tokenAudience != configAudience)
                    {
                        logger.LogWarning("Token audience validation failed. Expected: {Expected}, Actual: {Actual}", 
                            configAudience, tokenAudience);
                        
                        context.Fail("Invalid audience in token");
                        return Task.CompletedTask;
                    }
                    
                    logger.LogInformation("Token successfully validated for user {Subject} with audience {Audience}", 
                        token?.Subject, tokenAudience);
                    
                    return Task.CompletedTask;
                }
            };
            
            // Save token in authentication properties for easy access
            options.SaveToken = true;
        });

        // Add authorization
        services.AddAuthorization(options => {
            options.AddPolicy("EventGridPolicy", policy => {
                policy.RequireAuthenticatedUser();
                
                // Fix: Require specific audience claim with null safety
                string? audienceSetting = configuration["Authentication:Audience"];
                if (!string.IsNullOrEmpty(audienceSetting))
                {
                    policy.RequireClaim("aud", audienceSetting);
                }
                
                // Optional: If your 3PP provider includes specific scopes/permissions
                // Uncomment to require specific scope/permission:
                // policy.RequireClaim("scope", "eventgrid.events.write", "eventgrid.events.read");
                
                // Optional: Add role-based validation if your 3PP provider supports roles
                // policy.RequireRole("EventGridPublisher");
            });
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
