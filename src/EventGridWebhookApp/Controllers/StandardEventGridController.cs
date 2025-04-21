using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using EventGridWebhookApp.Models;
using EventGridWebhookApp.Services;
// Add necessary using statements
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer; // For default scheme constant
using Microsoft.AspNetCore.Authorization; // For IAuthorizationService and AuthorizeAsync
using System.Linq; // For FirstOrDefault() on header value


namespace EventGridWebhookApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StandardEventGridController : ControllerBase
{
    private readonly ILogger<StandardEventGridController> _logger;
    private readonly IEventValidationService _validationService; // Use interface
    private readonly IAuthorizationService _authorizationService; // Inject IAuthorizationService

    public StandardEventGridController(
        ILogger<StandardEventGridController> logger,
        IEventValidationService validationService, // Inject interface
        IAuthorizationService authorizationService) // Add IAuthorizationService here
    {
        _logger = logger;
        _validationService = validationService;
        _authorizationService = authorizationService; // Assign it
    }

    [HttpGet("test")]
    public IActionResult Test()
    {
        _logger.LogInformation("Test endpoint called at: {Time}", DateTime.UtcNow);
        
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown";
        _logger.LogInformation("Current environment: {Environment}", environment);
        
        return Ok(new { 
            message = "EventGrid API is working correctly", 
            timestamp = DateTime.UtcNow,
            environment = environment
        });
    }

    [HttpPost]
    // [Authorize(Policy = "EventGridPolicy")] // REMOVE this attribute
    public async Task<IActionResult> Post()
    {
        _logger.LogInformation("Received request on StandardEventGrid endpoint.");
        string requestBody;

        // Enable buffering to read the body multiple times if needed
        Request.EnableBuffering();

        // Check for validation handshake first (doesn't require auth)
        if (Request.Headers.TryGetValue("aeg-event-type", out var eventTypeHeader) &&
            eventTypeHeader.FirstOrDefault()?.Equals("SubscriptionValidation", StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogInformation("Detected SubscriptionValidation event type via header.");
            using (var reader = new StreamReader(Request.Body, leaveOpen: true)) // Keep stream open initially
            {
                requestBody = await reader.ReadToEndAsync();
            }
            // It's crucial to reset the stream position if the body might be read again later
            Request.Body.Position = 0;
            return await HandleValidationEvent(requestBody);
        }

        // --- If it's not a validation event, NOW check authorization ---
        // 1. Authenticate
        var authenticateResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme); // Use your configured scheme
        if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
        {
            _logger.LogWarning("Authentication failed for non-validation event. Scheme {Scheme}. Failure: {Failure}",
                JwtBearerDefaults.AuthenticationScheme,
                authenticateResult.Failure?.Message ?? "No details");
            return Unauthorized("Authentication required."); // 401
        }

        // 2. Authorize using the policy
        var authorizeResult = await _authorizationService.AuthorizeAsync(authenticateResult.Principal, "EventGridPolicy");
        if (!authorizeResult.Succeeded)
        {
            _logger.LogWarning("Authorization failed for policy 'EventGridPolicy' for user {User}.",
                authenticateResult.Principal.Identity?.Name ?? "unknown");
            return Forbid(); // 403
        }
        // --- End Authorization Check ---

        _logger.LogInformation("Authentication and Authorization successful for standard event processing.");

        try
        {
            // Read the request body (stream was reset after potential validation check)
            using (var reader = new StreamReader(Request.Body, leaveOpen: false)) // Can close stream now
            {
                requestBody = await reader.ReadToEndAsync();
            }

            // Parse the events
            var events = JsonSerializer.Deserialize<List<StandardEventGridEvent>>(requestBody);

            if (events == null || events.Count == 0)
            {
                _logger.LogWarning("Failed to deserialize events or event list is empty.");
                return BadRequest("Invalid event format or empty event list.");
            }

            // Validate signature if present (for non-validation events)
            if (Request.Headers.TryGetValue("aeg-signature", out var signature))
            {
                _logger.LogInformation("Found aeg-signature header. Validating signature.");
                if (!await _validationService.ValidateSignature(signature.ToString(), requestBody))
                {
                    _logger.LogWarning("Invalid event signature.");
                    return Unauthorized("Invalid signature"); // Keep 401 for bad signature
                }
                _logger.LogInformation("Signature validated successfully.");
            }
            else
            {
                _logger.LogInformation("No aeg-signature header found. Skipping signature validation.");
                // Consider if a missing signature should be an error depending on your requirements
                // return BadRequest("Missing required aeg-signature header.");
            }


            foreach (var eventData in events)
            {
                // Validate topic
                if (eventData.Topic != null && !_validationService.IsValidTopic(eventData.Topic))
                {
                    _logger.LogWarning($"Unauthorized topic: {eventData.Topic}");
                    // Use Forbid (403) here as the user is authenticated but not authorized for this topic
                    return Forbid(); // Changed from Unauthorized
                }

                // Process each event
                _logger.LogInformation($"Processing event: {eventData.Id} of type {eventData.EventType}");

                // Add your event processing logic here
                // ...
            }

            return Ok();
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "Error deserializing standard event JSON");
            return BadRequest("Invalid JSON format for standard event.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing standard EventGrid event");
            return StatusCode(500, "Internal server error");
        }
    }

    private async Task<IActionResult> HandleValidationEvent(string requestBody)
    {
        _logger.LogInformation("Handling validation event");

        try
        {
            // Event Grid sends validation events in an array
            var events = JsonSerializer.Deserialize<List<StandardEventGridEvent>>(requestBody);

            // Find the validation event within the list (case-insensitive check just in case)
            var validationEvent = events?.FirstOrDefault(e =>
                string.Equals(e.EventType, "Microsoft.EventGrid.SubscriptionValidationEvent", StringComparison.OrdinalIgnoreCase));

            if (validationEvent?.Data is JsonElement data &&
                data.TryGetProperty("validationCode", out var validationCodeElement) &&
                validationCodeElement.ValueKind == JsonValueKind.String)
            {
                var validationCode = validationCodeElement.GetString();
                if (!string.IsNullOrEmpty(validationCode))
                {
                    _logger.LogInformation("Extracted validation code. Returning validation response.");

                    // Call the service method if it needs to do anything specific during validation
                    await _validationService.HandleSubscriptionValidationEvent(requestBody);

                    return Ok(new { validationResponse = validationCode });
                }
                else
                {
                    _logger.LogWarning("Validation code property found but was null or empty.");
                }
            }
            else
            {
                 // --- Fix: Check if validationEvent or validationEvent.Data is null before accessing ToString() ---
                 string eventDataString = validationEvent?.Data is JsonElement dataElement ? dataElement.ToString() : "null";
                 _logger.LogWarning("Validation event structure invalid or validationCode property not found/not a string. Event Data: {EventData}", eventDataString);
                 // --- End Fix ---
            }


            _logger.LogWarning("Failed to process validation event correctly. Request body: {RequestBody}", requestBody);
            // Avoid echoing potentially large/sensitive request body back in response
            return BadRequest("Unable to process validation event. Check logs for details.");
        }
        catch (JsonException jsonEx)
        {
             _logger.LogError(jsonEx, "Error deserializing validation event JSON: {RequestBody}", requestBody);
             return BadRequest("Invalid JSON format for validation event.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error handling validation event: {RequestBody}", requestBody);
            return StatusCode(500, "Internal server error processing validation event");
        }
    }
}