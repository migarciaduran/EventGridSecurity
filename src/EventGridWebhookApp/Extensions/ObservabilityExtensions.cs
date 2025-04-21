using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace EventGridWebhookApp.Extensions;

public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        string? appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"] ??
                                             Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName: builder.Environment.ApplicationName,
                        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown");

        // Configure OpenTelemetry Logging
        builder.Logging.ClearProviders();
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resourceBuilder);
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.ParseStateValues = true;
            options.AddConsoleExporter(); // Keep console for local dev
            if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
            {
                options.AddAzureMonitorLogExporter(opt => opt.ConnectionString = appInsightsConnectionString);
            }
        });

        // Configure OpenTelemetry Tracing and Metrics
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(rb => rb.AddService(
                        serviceName: builder.Environment.ApplicationName,
                        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown"))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
                {
                    tracing.AddAzureMonitorTraceExporter(options => options.ConnectionString = appInsightsConnectionString);
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation();
                // Add other relevant meters if needed, e.g., .AddMeter("MyCustomMeter")
                if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
                {
                    metrics.AddAzureMonitorMetricExporter(options => options.ConnectionString = appInsightsConnectionString);
                }
            });

        // Add Application Insights SDK for richer features (Live Metrics, Profiler, etc.)
        if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
        {
            builder.Services.AddApplicationInsightsTelemetry(options =>
            {
                options.ConnectionString = appInsightsConnectionString;
            });
        }

        return builder;
    }
}
