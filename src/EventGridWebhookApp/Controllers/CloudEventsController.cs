using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using EventGridWebhookApp.Models;
using EventGridWebhookApp.Services;
using Microsoft.AspNetCore.Authorization;

namespace EventGridWebhookApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CloudEventsController : ControllerBase
    {
        private readonly ILogger<CloudEventsController> _logger;
        private readonly IEventValidationService _validationService; // Use interface
        private readonly IAuthorizationService _authorizationService; // Add authorization service

        public CloudEventsController(
            ILogger<CloudEventsController> logger,
            IEventValidationService validationService,
            IAuthorizationService authorizationService) // Inject interface
        {
            _logger = logger;
            _validationService = validationService;
            _authorizationService = authorizationService;
        }

        [HttpPost]
        [Authorize(Policy = "EventGridPolicy")]
        public async Task<IActionResult> Post()
        {
            _logger.LogInformation("Received CloudEvent");

            try
            {
                // Read the request body
                using var reader = new StreamReader(Request.Body);
                var requestBody = await reader.ReadToEndAsync();

                // Parse the CloudEvent
                var cloudEvent = JsonSerializer.Deserialize<CloudEvent>(requestBody);

                if (cloudEvent == null)
                {
                    _logger.LogWarning("Failed to deserialize CloudEvent");
                    return BadRequest("Invalid CloudEvent format");
                }

                // Validate signature if present
                if (Request.Headers.TryGetValue("ce-signature", out var signature))
                {
                    if (!await _validationService.ValidateSignature(signature.ToString(), requestBody)) // Await the async call
                    {
                        _logger.LogWarning("Invalid event signature");
                        return Unauthorized("Invalid signature");
                    }
                }

                // Validate source
                if (!_validationService.IsValidTopic(cloudEvent.Source))
                {
                    _logger.LogWarning($"Unauthorized source: {cloudEvent.Source}");
                    return Unauthorized("Unauthorized event source");
                }

                // Process the event
                _logger.LogInformation($"Processing CloudEvent: {cloudEvent.Id} of type {cloudEvent.Type}");

                // Add your event processing logic here
                // ...

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CloudEvent");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpOptions]
        public IActionResult Options()
        {
            _logger.LogInformation("Received OPTIONS request for CloudEvents webhook validation");
            
            // Get Origin header to validate it comes from Azure EventGrid
            if (!Request.Headers.TryGetValue("Origin", out var originValues) || originValues.Count == 0)
            {
                _logger.LogWarning("OPTIONS request missing Origin header");
                return BadRequest("Missing Origin header");
            }
            
            var origin = originValues.First();
            if (!string.Equals(origin, "azure-eventing.net", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("OPTIONS request from unauthorized origin: {Origin}", origin);
                return BadRequest("Unauthorized origin");
            }
            
            // Origin validated successfully - proceed with the CloudEvents validation handshake
            _logger.LogInformation("Valid origin confirmed: {Origin}", origin);
            
            // Set the proper response headers for the CloudEvents validation handshake
            Response.Headers["WebHook-Allowed-Origin"] = "azure-eventing.net";
            Response.Headers["WebHook-Allowed-Rate"] = "120";
            
            return Ok();
        }
    }
}