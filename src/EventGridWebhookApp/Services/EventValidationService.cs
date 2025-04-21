using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EventGridWebhookApp.Services
{
    /// <summary>
    /// Service for validating EventGrid events and CloudEvents
    /// </summary>
    public class EventValidationService : IEventValidationService // Implement the interface
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EventValidationService> _logger;

        public EventValidationService(IConfiguration configuration, ILogger<EventValidationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Validates the signature of an incoming event.
        /// </summary>
        /// <param name="signature">The signature from the request headers</param>
        /// <param name="eventData">The raw event data</param>
        /// <returns>True if the signature is valid, false otherwise</returns>
        public async Task<bool> ValidateSignature(string signature, string eventData) // Make async Task<bool>
        {
            try
            {
                // Get the validation key from configuration (stored securely in Key Vault)
                var validationKey = _configuration["EventGrid--ValidationKey"];
                if (string.IsNullOrEmpty(validationKey))
                {
                    _logger.LogWarning("EventGrid validation key is not configured");
                    return false;
                }

                // For EventGrid events, the signature is a SHA256 hash of the event data using the validation key
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(validationKey));
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(eventData));
                var computedSignature = Convert.ToBase64String(computedHash);

                // Compare the computed signature with the provided signature
                return await Task.FromResult(string.Equals(signature, computedSignature, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating event signature");
                return false;
            }
        }

        /// <summary>
        /// Validates that the topic/source is allowed.
        /// </summary>
        /// <param name="topic">The topic or source of the event</param>
        /// <returns>True if the topic is allowed, false otherwise</returns>
        public bool IsValidTopic(string topic)
        {
            try
            {
                // Try to get the allowed topics individually from Key Vault
                var allowedTopics = new List<string>();
                
                // Attempt to get at least one allowed topic (more can be added as needed)
                var allowedTopic0 = _configuration["EventGrid--AllowedTopics--0"];
                if (!string.IsNullOrEmpty(allowedTopic0))
                {
                    allowedTopics.Add(allowedTopic0);
                }
                
                // In development, we might have topics specified differently in local configuration
                if (allowedTopics.Count == 0)
                {
                    // Fall back to the old style configuration if needed
                    var configTopics = _configuration.GetSection("EventGrid:AllowedTopics").Get<string[]>();
                    if (configTopics != null && configTopics.Length > 0)
                    {
                        allowedTopics.AddRange(configTopics);
                    }
                }
                
                if (allowedTopics.Count == 0)
                {
                    _logger.LogWarning("No allowed topics configured");
                    return false;
                }

                // Check if the topic is in the list of allowed topics
                foreach (var allowedTopic in allowedTopics)
                {
                    if (topic.StartsWith(allowedTopic, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                _logger.LogWarning("Topic {Topic} is not in the list of allowed topics", topic);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating event topic");
                return false;
            }
        }

        /// <summary>
        /// Handles the EventGrid subscription validation event.
        /// </summary>
        /// <param name="requestBody">The raw request body containing the validation event</param>
        /// <returns>True if the validation event was handled successfully, false otherwise</returns>
        public async Task<bool> HandleSubscriptionValidationEvent(string requestBody)
        {
            // Placeholder implementation - In a real scenario, you would parse the requestBody,
            // extract the validation code, and return it in the response.
            // For now, just log and return true to simulate successful handling.
            _logger.LogInformation("Handling subscription validation event.");
            // Example parsing (requires Newtonsoft.Json or System.Text.Json):
            // var events = System.Text.Json.JsonSerializer.Deserialize<List<StandardEventGridEvent>>(requestBody);
            // if (events != null && events.Count > 0 && events[0].EventType == "Microsoft.EventGrid.SubscriptionValidationEvent")
            // {
            //     var validationData = events[0].Data as System.Text.Json.JsonElement?;
            //     if (validationData.HasValue && validationData.Value.TryGetProperty("validationCode", out var validationCodeElement))
            //     {
            //         var validationCode = validationCodeElement.GetString();
            //         _logger.LogInformation("Validation code received: {ValidationCode}", validationCode);
            //         // Respond with validation code - This needs to be done in the controller
            //         return true; 
            //     }
            // }
            await Task.CompletedTask; // Keep async signature
            return true; // Simulate success for now
        }
    }
}