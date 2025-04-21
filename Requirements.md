# EventGrid Webhook Application Requirements

I want to create a .NET Core application with the following features:

- The application should include two controllers:
  1. A controller for handling standard EventGrid schema events
  2. A dedicated controller for handling CloudEvents v1.0 schema
- The application should implement proper metrics and logging to monitor incoming requests
- Logging data should be formatted to enable visualization in the Azure portal
- The application should implement authentication and authorization for secure access
- We will use a service principal application in Azure portal for authentication and authorization

## Technical Requirements
- .NET 8 framework
- Azure EventGrid integration
- Logging and metrics implementation
- Azure portal compatibility for monitoring
- Deployment to Azure App Services
- CI/CD using GitHub Actions for automated deployment to Azure

## Event Schema and Processing
- Support for both standard EventGrid schema and CloudEvents v1.0 schema through separate controllers
- Standard EventGrid schema controller for initial testing
- CloudEvents v1.0 schema controller for forward compatibility and schema validation
- Reference CloudEvents v1.0 schema documentation: https://learn.microsoft.com/en-us/azure/event-grid/cloud-event-schema

## Controller Structure
- StandardEventGridController: Handles events in the native EventGrid schema
- CloudEventsController: Handles events conforming to CloudEvents v1.0 specification
- Both controllers should implement the same authentication mechanisms for comparison

## Logging Requirements
- Implement logging with appropriate detail level for a demonstration application
- Ensure logging provides good visibility into application operations and request processing
- Include request timestamps, authentication results, and basic request processing information
- Log schema validation results and parsing performance metrics

## Authentication and Security
- Implement secure webhook endpoint with proper validation
- Integrate with third-party authentication application in Azure portal
- Ensure secure communication channel for all webhook interactions
- Test and compare different authentication methods available in EventGrid
- Document authentication performance and security implications

## Prototype Enhancement Goals
- Implement modular authentication handlers to easily swap between different authentication methods
- Add configuration options to toggle between authentication strategies
- Create a dashboard or report to compare authentication methods
- Include sample CloudEvents v1.0 schema validation for future compatibility
- Add telemetry to measure authentication performance and reliability
- Create a simple UI to visualize authentication test results
- Compare performance and reliability between standard EventGrid schema and CloudEvents v1.0 schema
