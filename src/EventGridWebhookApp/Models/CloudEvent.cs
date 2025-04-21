using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace EventGridWebhookApp.Models
{
    /// <summary>
    /// Represents a CloudEvent as per the CloudEvents v1.0 schema.
    /// </summary>
    public class CloudEvent
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("source")]
        public string Source { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("specversion")]
        public string SpecVersion { get; set; } = "1.0";

        [JsonProperty("time")]
        public DateTime? Time { get; set; }

        [JsonProperty("subject")]
        public string? Subject { get; set; }

        [JsonProperty("datacontenttype")]
        public string? DataContentType { get; set; }

        [JsonProperty("data")]
        public object? Data { get; set; }

        [JsonProperty("dataschema")]
        public string? DataSchema { get; set; }
    }
}