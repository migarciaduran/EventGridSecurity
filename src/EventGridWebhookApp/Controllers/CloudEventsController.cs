using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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

        public CloudEventsController(
            ILogger<CloudEventsController> logger,
            IEventValidationService validationService) // Inject interface
        {
            _logger = logger;
            _validationService = validationService;
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
            // Handle OPTIONS requests for CloudEvents HTTP binding
            Response.Headers["WebHook-Allowed-Origin"] = "*";
            Response.Headers["WebHook-Allowed-Rate"] = "120";
            return Ok();
        }
    }
}