using System;
using System.Collections.Generic;
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
    public class StandardEventGridController : ControllerBase
    {
        private readonly ILogger<StandardEventGridController> _logger;
        private readonly IEventValidationService _validationService; // Use interface

        public StandardEventGridController(
            ILogger<StandardEventGridController> logger, 
            IEventValidationService validationService) // Inject interface
        {
            _logger = logger;
            _validationService = validationService;
        }

        [HttpPost]
        [Authorize(Policy = "EventGridPolicy")]
        public async Task<IActionResult> Post()
        {
            _logger.LogInformation("Received EventGrid event");

            try
            {
                // Read the request body
                using var reader = new StreamReader(Request.Body);
                var requestBody = await reader.ReadToEndAsync();
                
                // Check if this is a validation event
                if (requestBody.Contains("Microsoft.EventGrid.SubscriptionValidationEvent"))
                {
                    return await HandleValidationEvent(requestBody);
                }

                // Parse the events
                var events = JsonSerializer.Deserialize<List<StandardEventGridEvent>>(requestBody);
                
                if (events == null)
                {
                    _logger.LogWarning("Failed to deserialize events");
                    return BadRequest("Invalid event format");
                }

                // Validate signature if present
                if (Request.Headers.TryGetValue("aeg-signature", out var signature))
                {
                    if (!await _validationService.ValidateSignature(signature.ToString(), requestBody)) // Await the async call
                    {
                        _logger.LogWarning("Invalid event signature");
                        return Unauthorized("Invalid signature");
                    }
                }

                foreach (var eventData in events)
                {
                    // Validate topic
                    if (eventData.Topic != null && !_validationService.IsValidTopic(eventData.Topic))
                    {
                        _logger.LogWarning($"Unauthorized topic: {eventData.Topic}");
                        return Unauthorized("Unauthorized topic");
                    }

                    // Process each event
                    _logger.LogInformation($"Processing event: {eventData.Id} of type {eventData.EventType}");
                    
                    // Add your event processing logic here
                    // ...
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing EventGrid event");
                return StatusCode(500, "Internal server error");
            }
        }

        private async Task<IActionResult> HandleValidationEvent(string requestBody)
        {
            _logger.LogInformation("Handling validation event");
            
            try
            {
                var events = JsonSerializer.Deserialize<List<StandardEventGridEvent>>(requestBody);
                
                if (events == null)
                {
                    return BadRequest("Invalid validation event format");
                }

                foreach (var eventData in events)
                {
                    if (eventData.EventType == "Microsoft.EventGrid.SubscriptionValidationEvent")
                    {
                        var data = eventData.Data as JsonElement?;
                        if (data.HasValue && data.Value.TryGetProperty("validationCode", out var validationCodeElement))
                        {
                            var validationCode = validationCodeElement.GetString();
                            _logger.LogInformation($"Returning validation code: {validationCode}");
                            
                            // Call the service method (even if it's just logging for now)
                            await _validationService.HandleSubscriptionValidationEvent(requestBody);

                            return Ok(new
                            {
                                validationResponse = validationCode
                            });
                        }
                    }
                }
                
                return BadRequest("Validation event not found or validation code missing");
            }
            catch (JsonException jsonEx)
            {
                 _logger.LogError(jsonEx, "Error deserializing validation event JSON");
                 return BadRequest("Invalid JSON format for validation event.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling validation event");
                return StatusCode(500, "Internal server error processing validation event");
            }
        }
    }
}