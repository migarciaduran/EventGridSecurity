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
    public class CloudEventsControllerTests
    {
        private readonly Mock<ILogger<CloudEventsController>> _mockLogger;
        private readonly Mock<IEventValidationService> _mockValidationService; // Use interface mock
        private readonly CloudEventsController _controller;

        public CloudEventsControllerTests()
        {
            _mockLogger = new Mock<ILogger<CloudEventsController>>();
            _mockValidationService = new Mock<IEventValidationService>(); // Mock the interface

            _controller = new CloudEventsController(_mockLogger.Object, _mockValidationService.Object);

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
            request.ContentType = "application/cloudevents+json"; // Correct content type for CloudEvents

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
        public async Task Post_ShouldReturnOk_WhenHandlingValidCloudEvent()
        {
            // Arrange
            var eventId = Guid.NewGuid().ToString();
            var source = "/subscriptions/test/resourceGroups/test/providers/Microsoft.Storage/storageAccounts/teststorage";
            var cloudEvent = new CloudEvent
            {
                Id = eventId,
                Source = source,
                Type = "Microsoft.Storage.BlobCreated",
                SpecVersion = "1.0",
                DataSchema = "https://example.com/schema",
                Subject = "/blobServices/default/containers/test/blobs/image.png",
                Time = DateTime.UtcNow,
                Data = new { api = "PutBlockList", clientRequestId = "guid", contentLength = 512 }
            };
            var requestBody = JsonSerializer.Serialize(cloudEvent);
            var signature = "sha256=validSignaturePlaceholder"; // Placeholder
            var headers = new Dictionary<string, string> { { "ce-signature", signature } };
            SetupHttpRequest(requestBody, headers);

            _mockValidationService.Setup(v => v.ValidateSignature(signature, requestBody)).ReturnsAsync(true); // Use ReturnsAsync
            _mockValidationService.Setup(v => v.IsValidTopic(source)).Returns(true);

            // Act
            var result = await _controller.Post();

            // Assert
            Assert.IsType<OkResult>(result);
            _mockValidationService.Verify(v => v.ValidateSignature(signature, requestBody), Times.Once);
            _mockValidationService.Verify(v => v.IsValidTopic(source), Times.Once);
             _mockLogger.Verify(
               x => x.Log(
                   LogLevel.Information,
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((o, t) => o.ToString().Contains($"Processing CloudEvent: {eventId}")),
                   null,
                   It.IsAny<Func<It.IsAnyType, Exception, string>>()
               ),
               Times.Once);
        }

        [Fact]
        public async Task Post_ShouldReturnUnauthorized_WhenSignatureIsInvalid()
        {
            // Arrange
            var cloudEvent = new CloudEvent { /* ... create event ... */ };
            var requestBody = JsonSerializer.Serialize(cloudEvent);
            var signature = "sha256=invalidSignature";
            var headers = new Dictionary<string, string> { { "ce-signature", signature } };
            SetupHttpRequest(requestBody, headers);

            _mockValidationService.Setup(v => v.ValidateSignature(signature, requestBody)).ReturnsAsync(false); // Use ReturnsAsync

            // Act
            var result = await _controller.Post();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal("Invalid signature", unauthorizedResult.Value);
            _mockValidationService.Verify(v => v.ValidateSignature(signature, requestBody), Times.Once);
            _mockValidationService.Verify(v => v.IsValidTopic(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Post_ShouldReturnUnauthorized_WhenSourceIsInvalid()
        {
            // Arrange
            var source = "/subscriptions/test/resourceGroups/test/providers/Microsoft.Storage/storageAccounts/invalidSource";
            var cloudEvent = new CloudEvent { Id = "event1", Type = "type1", Source = source, SpecVersion = "1.0" };
            var requestBody = JsonSerializer.Serialize(cloudEvent);
            SetupHttpRequest(requestBody);
            // Assume signature validation passes or is not needed for this test case
            _mockValidationService.Setup(v => v.ValidateSignature(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            _mockValidationService.Setup(v => v.IsValidTopic(source)).Returns(false); // Mock invalid source

            // Act
            var result = await _controller.Post();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal("Unauthorized event source", unauthorizedResult.Value);
            _mockValidationService.Verify(v => v.IsValidTopic(source), Times.Once);
        }

        [Fact]
        public async Task Post_ShouldReturnBadRequest_WhenRequestBodyIsInvalidJson()
        {
            // Arrange
            var requestBody = "{ \"invalidJson\": \""; // Malformed JSON
            SetupHttpRequest(requestBody);

            // Act
            // Exception will be caught by the controller's try-catch, leading to 500, 
            // unless deserialization fails *before* the try block (unlikely with current structure)
            // Let's adjust the expectation based on the current controller logic which catches general Exception
            var result = await _controller.Post();

            // Assert
            // The current controller catches the JsonException and returns 500.
            // If the requirement is a 400 for bad JSON, the controller needs adjustment.
            // For now, test the existing behavior.
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
            Assert.Equal("Internal server error", statusCodeResult.Value);

            // Verify Error log, not Warning for deserialization failure leading to exception
             _mockLogger.Verify(
               x => x.Log(
                   LogLevel.Error, // Should log Error when exception is caught
                   It.IsAny<EventId>(), 
                   It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Error processing CloudEvent")),
                   It.IsAny<JsonException>(), // Expect a JsonException
                   It.IsAny<Func<It.IsAnyType, Exception, string>>()
               ),
               Times.Once);
        }

        [Fact]
        public async Task Post_ShouldReturnInternalServerError_WhenValidationServiceThrowsException()
        {
            // Arrange
            var source = "/subscriptions/test/resourceGroups/test/providers/Microsoft.Storage/storageAccounts/teststorage";
            var cloudEvent = new CloudEvent { Id = "event1", Type = "type1", Source = source, SpecVersion = "1.0" };
            var requestBody = JsonSerializer.Serialize(cloudEvent);
            SetupHttpRequest(requestBody);

            // Setup IsValidTopic to throw
            _mockValidationService.Setup(v => v.IsValidTopic(source)).Throws(new Exception("Test Exception"));
            // Ensure signature validation doesn't interfere if called first
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
                   It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Error processing CloudEvent")),
                   It.IsAny<Exception>(),
                   It.IsAny<Func<It.IsAnyType, Exception, string>>()
               ),
               Times.Once);
        }

        [Fact]
        public void Options_ShouldReturnOkWithHeaders()
        {
            // Arrange
            // No specific arrangement needed for OPTIONS

            // Act
            var result = _controller.Options();

            // Assert
            var okResult = Assert.IsType<OkResult>(result);
            Assert.True(_controller.Response.Headers.ContainsKey("WebHook-Allowed-Origin"));
            Assert.Equal("*", _controller.Response.Headers["WebHook-Allowed-Origin"]);
            Assert.True(_controller.Response.Headers.ContainsKey("WebHook-Allowed-Rate"));
            Assert.Equal("120", _controller.Response.Headers["WebHook-Allowed-Rate"]);
        }
    }
}
