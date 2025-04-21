using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace EventGridWebhookApp.Models
{
    /// <summary>
    /// Represents a standard EventGrid event as per the Azure EventGrid schema.
    /// </summary>
    public class StandardEventGridEvent
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonProperty("data")]
        public object? Data { get; set; }

        [JsonProperty("eventType")]
        public string EventType { get; set; } = string.Empty;

        [JsonProperty("dataVersion")]
        public string DataVersion { get; set; } = string.Empty;

        [JsonProperty("metadataVersion")]
        public string? MetadataVersion { get; set; }

        [JsonProperty("eventTime")]
        public DateTime EventTime { get; set; }

        [JsonProperty("topic")]
        public string? Topic { get; set; }
    }
}