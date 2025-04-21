using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using EventGridWebhookApp.Services;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System;
using System.Threading.Tasks; // Add Task

namespace EventGridWebhookApp.Tests
{
    public class EventValidationServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<EventValidationService>> _mockLogger;
        private readonly EventValidationService _service;
        // Remove _mockConfigSection, mock values directly

        public EventValidationServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<EventValidationService>>();

            // Setup default configuration mock - No longer needed for GetSection setup here

            _service = new EventValidationService(_mockConfiguration.Object, _mockLogger.Object);
        }

        // --- IsValidTopic Tests ---

        [Fact]
        public void IsValidTopic_ShouldReturnTrue_WhenTopicIsAllowed()
        {
            // Arrange
            var allowedTopic = "/subscriptions/test-sub/resourceGroups/test-rg/providers/Microsoft.EventGrid/topics/test-topic";
            _mockConfiguration.Setup(c => c["EventGrid--AllowedTopics--0"]).Returns(allowedTopic);
            var eventTopic = allowedTopic + "/some/suffix"; // Topic can be more specific

            // Act
            var result = _service.IsValidTopic(eventTopic);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValidTopic_ShouldReturnFalse_WhenTopicIsNotAllowed()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["EventGrid--AllowedTopics--0"]).Returns("/subscriptions/allowed-sub/...");
            var eventTopic = "/subscriptions/disallowed-sub/resourceGroups/test-rg/providers/Microsoft.EventGrid/topics/test-topic";

            // Act
            var result = _service.IsValidTopic(eventTopic);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsValidTopic_ShouldReturnFalse_WhenNoTopicsConfigured()
        {
            // Arrange
             _mockConfiguration.Setup(c => c["EventGrid--AllowedTopics--0"]).Returns((string)null); // No primary config
             // Mock the fallback section and its children to return null/empty
             var mockFallbackSection = new Mock<IConfigurationSection>();
             // Remove setup for Exists()
             mockFallbackSection.Setup(s => s.Value).Returns((string)null);
             mockFallbackSection.Setup(s => s.GetChildren()).Returns(new List<IConfigurationSection>()); // No children
             _mockConfiguration.Setup(c => c.GetSection("EventGrid:AllowedTopics")).Returns(mockFallbackSection.Object);

            var eventTopic = "/subscriptions/any-sub/resourceGroups/test-rg/providers/Microsoft.EventGrid/topics/test-topic";

            // Act
            var result = _service.IsValidTopic(eventTopic);

            // Assert
            Assert.False(result);
            // Verify warning log
             _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("No allowed topics configured")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

         [Fact]
        public void IsValidTopic_ShouldUseFallbackConfiguration_WhenPrimaryIsNotSet()
        {
            // Arrange
            var allowedTopicFallback = "/subscriptions/fallback-sub/resourceGroups/test-rg/providers/Microsoft.EventGrid/topics/fallback-topic";
            _mockConfiguration.Setup(c => c["EventGrid--AllowedTopics--0"]).Returns((string)null); // No primary config

            // Mock the fallback section and its children
            var mockFallbackSection = new Mock<IConfigurationSection>();
            var mockChildSection = new Mock<IConfigurationSection>();
            mockChildSection.Setup(s => s.Value).Returns(allowedTopicFallback);
            var children = new List<IConfigurationSection> { mockChildSection.Object };
            // Remove setup for Exists()
            mockFallbackSection.Setup(s => s.GetChildren()).Returns(children);
            _mockConfiguration.Setup(c => c.GetSection("EventGrid:AllowedTopics")).Returns(mockFallbackSection.Object);

            var eventTopic = allowedTopicFallback;

            // Act
            var result = _service.IsValidTopic(eventTopic);

            // Assert
            Assert.True(result);
        }

        // --- ValidateSignature Tests ---

        [Fact]
        public async Task ValidateSignature_ShouldReturnTrue_WhenSignatureIsValid() // Make async
        {
            // Arrange
            var validationKey = "testValidationKey1234567890";
            var eventData = "{\"id\":\"test-id\",\"eventType\":\"test.event\",\"subject\":\"test-subject\",\"data\":{},\"eventTime\":\"2025-04-21T12:00:00Z\",\"dataVersion\":\"1.0\"}";
            _mockConfiguration.Setup(c => c["EventGrid--ValidationKey"]).Returns(validationKey);

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(validationKey));
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(eventData));
            var validSignature = Convert.ToBase64String(computedHash);

            // Act
            var result = await _service.ValidateSignature(validSignature, eventData); // Await result

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ValidateSignature_ShouldReturnFalse_WhenSignatureIsInvalid() // Make async
        {
            // Arrange
            var validationKey = "testValidationKey1234567890";
            var eventData = "{\"id\":\"test-id\",\"eventType\":\"test.event\",\"subject\":\"test-subject\",\"data\":{},\"eventTime\":\"2025-04-21T12:00:00Z\",\"dataVersion\":\"1.0\"}";
            var invalidSignature = "invalidBase64Signature=";
            _mockConfiguration.Setup(c => c["EventGrid--ValidationKey"]).Returns(validationKey);

            // Act
            var result = await _service.ValidateSignature(invalidSignature, eventData); // Await result

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidateSignature_ShouldReturnFalse_WhenValidationKeyIsNotConfigured() // Make async
        {
            // Arrange
            var eventData = "{\"id\":\"test-id\",\"eventType\":\"test.event\",\"subject\":\"test-subject\",\"data\":{},\"eventTime\":\"2025-04-21T12:00:00Z\",\"dataVersion\":\"1.0\"}";
            var signature = "anySignature";
            _mockConfiguration.Setup(c => c["EventGrid--ValidationKey"]).Returns((string)null); // Key not configured

            // Act
            var result = await _service.ValidateSignature(signature, eventData); // Await result

            // Assert
            Assert.False(result);
             // Verify warning log
             _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("EventGrid validation key is not configured")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>() // Corrected Func signature
                ),
                Times.Once);
        }

        // Add tests for HandleSubscriptionValidationEvent if needed
        [Fact]
        public async Task HandleSubscriptionValidationEvent_ShouldLogInformationAndReturnTrue()
        {
            // Arrange
            var requestBody = "{\"some\":\"data\"}"; // Body content doesn't matter for current placeholder impl

            // Act
            var result = await _service.HandleSubscriptionValidationEvent(requestBody);

            // Assert
            Assert.True(result);
            _mockLogger.Verify(
               x => x.Log(
                   LogLevel.Information,
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Handling subscription validation event")),
                   null,
                   It.IsAny<Func<It.IsAnyType, Exception, string>>()
               ),
               Times.Once);
        }
    }
}
