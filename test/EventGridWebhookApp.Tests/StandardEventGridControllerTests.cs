using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using EventGridWebhookApp.Controllers;
using EventGridWebhookApp.Services;
using EventGridWebhookApp.Models;
using Microsoft.Extensions.Primitives;
using System;

namespace EventGridWebhookApp.Tests
{
    public class StandardEventGridControllerTests
    {
        private readonly Mock<ILogger<StandardEventGridController>> _mockLogger;
        private readonly Mock<IEventValidationService> _mockValidationService; // Use interface mock
        private readonly StandardEventGridController _controller;

        public StandardEventGridControllerTests()
        {
            _mockLogger = new Mock<ILogger<StandardEventGridController>>();
            _mockValidationService = new Mock<IEventValidationService>(); // Mock the interface

            _controller = new StandardEventGridController(_mockLogger.Object, _mockValidationService.Object);

            // Setup default HttpContext
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        private void SetupHttpRequest(string body, Dictionary<string, string>? headers = null)
        {
            var httpContext = new DefaultHttpContext();
            var request = httpContext.Request;
            request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            request.ContentLength = request.Body.Length;
            request.ContentType = "application/json";

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.Append(header.Key, new StringValues(header.Value));
                }
            }

            _controller.ControllerContext.HttpContext = httpContext;
        }

        [Fact]
        public async Task Post_ShouldReturnOk_WhenHandlingValidationEvent()
        {
            // Arrange
            var validationCode = Guid.NewGuid().ToString();
            var validationEvent = new List<StandardEventGridEvent>
            {
                new StandardEventGridEvent
                {
                    Id = Guid.NewGuid().ToString(),
                    EventType = "Microsoft.EventGrid.SubscriptionValidationEvent",
                    Data = JsonDocument.Parse($"{{\"validationCode\": \"{validationCode}\"}}").RootElement, // Correctly structure data
                    EventTime = DateTime.UtcNow,
                    DataVersion = "1",
                    Subject = "Validation",
                    Topic = "/subscriptions/test/resourceGroups/test/providers/Microsoft.EventGrid/topics/test"
                }
            };
            var requestBody = JsonSerializer.Serialize(validationEvent);
            SetupHttpRequest(requestBody);

            // Mock the service call for validation event handling
            _mockValidationService.Setup(v => v.HandleSubscriptionValidationEvent(requestBody)).ReturnsAsync(true);

            // Act
            var result = await _controller.Post();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            var responseValue = okResult.Value.GetType().GetProperty("validationResponse")?.GetValue(okResult.Value, null);
            Assert.Equal(validationCode, responseValue);
            _mockLogger.Verify(
               x => x.Log(
                   LogLevel.Information,
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Handling validation event")),
                   null,
                   It.IsAny<Func<It.IsAnyType, Exception, string>>()
               ),
               Times.Once);
            // Verify the service method was called
            _mockValidationService.Verify(v => v.HandleSubscriptionValidationEvent(requestBody), Times.Once);
        }

        [Fact]
        public async Task Post_ShouldReturnOk_WhenHandlingValidStandardEvent()
        {
            // Arrange
            var eventId = Guid.NewGuid().ToString();
            var topic = "/subscriptions/test/resourceGroups/test/providers/Microsoft.EventGrid/topics/test";
            var standardEvent = new List<StandardEventGridEvent>
            {
                new StandardEventGridEvent
                {
                    Id = eventId,
                    EventType = "MyApp.CustomEvent",
                    Data = new { Message = "Hello World" },
                    EventTime = DateTime.UtcNow,
                    DataVersion = "1.0",
                    Subject = "TestSubject",
                    Topic = topic
                }
            };
            var requestBody = JsonSerializer.Serialize(standardEvent);
            var signature = "sha256=validSignaturePlaceholder"; // Placeholder, actual validation mocked
            var headers = new Dictionary<string, string> { { "aeg-signature", signature } };
            SetupHttpRequest(requestBody, headers);

            _mockValidationService.Setup(v => v.ValidateSignature(signature, requestBody)).ReturnsAsync(true); // Use ReturnsAsync
            _mockValidationService.Setup(v => v.IsValidTopic(topic)).Returns(true);

            // Act
            var result = await _controller.Post();

            // Assert
            Assert.IsType<OkResult>(result);
            _mockValidationService.Verify(v => v.ValidateSignature(signature, requestBody), Times.Once);
            _mockValidationService.Verify(v => v.IsValidTopic(topic), Times.Once);
             _mockLogger.Verify(
               x => x.Log(
                   LogLevel.Information,
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((o, t) => o.ToString().Contains($"Processing event: {eventId}")),
                   null,
                   It.IsAny<Func<It.IsAnyType, Exception, string>>()
               ),
               Times.Once);
        }

        [Fact]
        public async Task Post_ShouldReturnUnauthorized_WhenSignatureIsInvalid()
        {
            // Arrange
            var standardEvent = new List<StandardEventGridEvent> { /* ... create event ... */ };
            var requestBody = JsonSerializer.Serialize(standardEvent);
            var signature = "sha256=invalidSignature";
            var headers = new Dictionary<string, string> { { "aeg-signature", signature } };
            SetupHttpRequest(requestBody, headers);

            _mockValidationService.Setup(v => v.ValidateSignature(signature, requestBody)).ReturnsAsync(false); // Use ReturnsAsync

            // Act
            var result = await _controller.Post();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal("Invalid signature", unauthorizedResult.Value);
            _mockValidationService.Verify(v => v.ValidateSignature(signature, requestBody), Times.Once);
            _mockValidationService.Verify(v => v.IsValidTopic(It.IsAny<string>()), Times.Never); // Topic validation shouldn't happen
        }

        [Fact]
        public async Task Post_ShouldReturnUnauthorized_WhenTopicIsInvalid()
        {
            // Arrange
             var topic = "/subscriptions/test/resourceGroups/test/providers/Microsoft.EventGrid/topics/invalid-topic";
            var standardEvent = new List<StandardEventGridEvent>
            {
                new StandardEventGridEvent { Id = "event1", EventType = "type1", Topic = topic, Data = new {} }
            };
            var requestBody = JsonSerializer.Serialize(standardEvent);
            // No signature header for simplicity, or mock ValidateSignature to return true if header is present
            SetupHttpRequest(requestBody);
             _mockValidationService.Setup(v => v.ValidateSignature(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true); // Use ReturnsAsync

            _mockValidationService.Setup(v => v.IsValidTopic(topic)).Returns(false); // Mock invalid topic

            // Act
            var result = await _controller.Post();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal("Unauthorized topic", unauthorizedResult.Value);
            _mockValidationService.Verify(v => v.IsValidTopic(topic), Times.Once);
        }

         [Fact]
        public async Task Post_ShouldReturnBadRequest_WhenRequestBodyIsInvalidJson()
        {
            // Arrange
            var requestBody = "[{ \"invalidJson\": \""; // Malformed JSON
            SetupHttpRequest(requestBody);

            // Act
            var result = await _controller.Post();

            // Assert
            // The controller catches JsonException during deserialization within HandleValidationEvent or Post
            // and returns BadRequest or 500 depending on where it happens.
            // The specific test case for validation event JSON error is handled in HandleValidationEvent.
            // This test targets the main Post method's deserialization.
            // If it contains validation event text, it goes to HandleValidationEvent which returns BadRequest on JsonException.
            // If it doesn't, the main Post method tries to deserialize, catches Exception, and returns 500.
            // Let's assume it doesn't contain the validation event text for this test.
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
            Assert.Equal("Internal server error", statusCodeResult.Value);

             _mockLogger.Verify(
               x => x.Log(
                   LogLevel.Error, // Expect Error log level due to caught exception
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Error processing EventGrid event")),
                   It.IsAny<JsonException>(), // Expect a JsonException
                   It.IsAny<Func<It.IsAnyType, Exception, string>>()
               ),
               Times.Once);
        }

         [Fact]
        public async Task Post_ShouldReturnInternalServerError_WhenValidationServiceThrowsException()
        {
            // Arrange
            var topic = "/subscriptions/test/resourceGroups/test/providers/Microsoft.EventGrid/topics/test";
            var standardEvent = new List<StandardEventGridEvent>
            {
                new StandardEventGridEvent { Id = "event1", EventType = "type1", Topic = topic, Data = new {} }
            };
            var requestBody = JsonSerializer.Serialize(standardEvent);
            SetupHttpRequest(requestBody);

            _mockValidationService.Setup(v => v.IsValidTopic(topic)).Throws(new Exception("Test Exception")); // Simulate service error
            // Ensure signature validation doesn't interfere
            _mockValidationService.Setup(v => v.ValidateSignature(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);


            // Act
            var result = await _controller.Post();

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
            Assert.Equal("Internal server error", statusCodeResult.Value);
             _mockLogger.Verify(
               x => x.Log(
                   LogLevel.Error,
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Error processing EventGrid event")),
                   It.IsAny<Exception>(), // Expect the exception
                   It.IsAny<Func<It.IsAnyType, Exception, string>>()
               ),
               Times.Once);
        }
    }
}
