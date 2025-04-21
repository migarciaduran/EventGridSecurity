using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EventGridWebhookApp.Models
{
    /// <summary>
    /// Represents a standard EventGrid event as per the Azure EventGrid schema.
    /// Uses System.Text.Json attributes.
    /// </summary>
    public class StandardEventGridEvent
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public object? Data { get; set; }

        [JsonPropertyName("eventType")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("dataVersion")]
        public string DataVersion { get; set; } = string.Empty;

        [JsonPropertyName("metadataVersion")]
        public string? MetadataVersion { get; set; }

        [JsonPropertyName("eventTime")]
        public DateTime EventTime { get; set; }

        [JsonPropertyName("topic")]
        public string? Topic { get; set; }
    }
}