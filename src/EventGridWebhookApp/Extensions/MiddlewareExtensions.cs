using EventGridWebhookApp.Services; // Assuming ExceptionHandlingMiddleware is here

namespace EventGridWebhookApp.Extensions;

public static class MiddlewareExtensions
{
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        // Add global exception handling middleware early in the pipeline
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        app.UseSwagger();
        app.UseSwaggerUI();

        // Redirect root path requests to Swagger UI in Development
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/")
            {
                context.Response.Redirect("/swagger");
                return; // Short-circuit the pipeline
            }
            await next(context); // Pass context explicitly
        });
        
        // Configure the HTTP request pipeline based on environment
        if (!app.Environment.IsDevelopment())
        {
            // Add security headers in production
            app.UseHsts(); // Enforces HTTPS
        }

        // Standard middleware
        app.UseHttpsRedirection(); // Redirect HTTP to HTTPS

        // Use CORS - Must be placed after UseRouting (implicitly added by MapControllers)
        // and before UseAuthentication/UseAuthorization
        app.UseCors("EventGridCorsPolicy");

        // Use authentication and authorization
        app.UseAuthentication(); // Identifies the user
        app.UseAuthorization(); // Verifies user permissions

        // Use rate limiting - Place after AuthN/AuthZ if limits depend on user identity
        app.UseRateLimiter();

        // Map controllers - This implicitly adds UseRouting and UseEndpoints
        app.MapControllers();

        // Map the health check endpoint
        app.MapHealthChecks("/healthz");

        return app;
    }
}
