# EventGrid Webhook Application

This is a secure .NET 8 web application that processes Azure EventGrid events using both the standard EventGrid schema and CloudEvents schema.

## Security Features

- JWT Bearer Authentication
- Authorization policies
- Input validation
- Signature verification
- Rate limiting
- CORS policy configuration
- HTTPS enforcement
- Global exception handling
- Secure secret management with Azure Key Vault

## EventGrid Secure Webhook Delivery

This application is configured to work with Azure EventGrid Secure Webhook Delivery using Microsoft Entra ID (Azure AD) authentication:

1. The application validates tokens sent by Event Grid with:
   - Verification of the issuer (Azure AD)
   - Validation of the audience claim (your app's URI)
   - Confirmation of the appid claim (Event Grid's app ID: 4962773b-9cdb-44cf-a8bf-237846a00ab7)
   - Token expiration and signature validation

2. To configure EventGrid to send authenticated requests:
   - In the Azure portal, when creating your Event Subscription, go to "Additional Features"
   - Enable "Use Microsoft Entra Authentication"
   - Provide your Azure AD tenant ID and application ID or App ID URI
   - Ensure Event Grid has been granted the "AzureEventGridSecureWebhookSubscriber" role on your application

## Prerequisites

- .NET 8 SDK
- Azure subscription
- Azure CLI
- GitHub account (for GitHub Actions deployment)

## Local Development Setup

1. Clone the repository
2. Create local configuration file (`appsettings.Development.json`) with the following structure:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Authentication": {
    "Authority": "https://login.microsoftonline.com/YOUR_TENANT_ID/",
    "Audience": "YOUR_APP_ID_URI",
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET"
  },
  "EventGrid": {
    "ValidationKey": "YOUR_VALIDATION_KEY",
    "AllowedTopics": [
      "/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.EventGrid/topics/{topic-name}"
    ]
  }
}
```

3. Run the application:
```bash
cd EventGridWebhookApp
dotnet run
```

## Deployment with GitHub Actions

This project includes a GitHub Actions workflow that:
1. Builds the .NET application
2. Deploys Azure infrastructure using Bicep templates
3. Configures Key Vault with secrets
4. Deploys the application to Azure App Service

### Setting up GitHub Secrets

The following secrets need to be created in your GitHub repository:

| Secret Name | Description |
|-------------|-------------|
| `AZURE_CLIENT_ID` | Client ID of the Service Principal with permission to deploy to Azure |
| `AZURE_TENANT_ID` | Tenant ID of your Azure subscription |
| `AZURE_SUBSCRIPTION_ID` | Azure Subscription ID |
| `AZURE_RESOURCE_GROUP` | Name of the resource group to deploy to |
| `RESOURCE_NAME_PREFIX` | Prefix for all Azure resource names |
| `AUTHORITY_URL` | Authority URL for authentication (e.g., `https://login.microsoftonline.com/YOUR_TENANT_ID/`) |
| `AUDIENCE_URL` | Audience URL for JWT validation |
| `CLIENT_ID` | Azure AD client ID for authentication |
| `CLIENT_SECRET` | Azure AD client secret for authentication |
| `EVENT_GRID_VALIDATION_KEY` | Key for validating EventGrid signatures |

### GitHub Actions Authentication to Azure

1. Create a Service Principal for GitHub Actions:
```bash
az ad sp create-for-rbac --name "GitHubActions-EventGridApp" --role contributor \
  --scopes /subscriptions/{subscription-id}/resourceGroups/{resource-group-name} \
  --sdk-auth
```

2. Save the output JSON as GitHub secrets:
   - `AZURE_CLIENT_ID` = appId
   - `AZURE_TENANT_ID` = tenant
   - `AZURE_SUBSCRIPTION_ID` = Your Azure subscription ID

### Manual Deployment

To deploy manually using Azure CLI:

1. Deploy the Bicep template:
```bash
az deployment group create \
  --resource-group YOUR_RESOURCE_GROUP \
  --template-file ./Infrastructure/main.bicep \
  --parameters environmentName=dev resourceNamePrefix=YOUR_PREFIX
```

2. Set the required Key Vault secrets
3. Publish and deploy the application to Azure Web App

## API Endpoints

- `/api/standardeventgrid` - Accepts standard Azure EventGrid schema events
- `/api/cloudevents` - Accepts CloudEvents v1.0 schema events

## Solution Architecture

- **EventGridWebhookApp**: Main application with controllers, models, and services
- **Infrastructure**: Bicep templates for infrastructure as code
  - **main.bicep**: Main deployment template
  - **Modules**: Reusable Bicep modules for different Azure resources

## Security Best Practices

1. **Secret Management**: All secrets are stored in Azure Key Vault and accessed via Key Vault references
2. **Authentication**: Uses Azure AD for authentication
3. **Signature Validation**: Validates EventGrid event signatures
4. **Topic Validation**: Restricts events to allowed topics/sources
5. **Infrastructure as Code**: Uses Bicep templates for consistent, secure deployments
6. **HTTPS Only**: Enforces HTTPS communications
7. **TLS 1.2+**: Enforces minimum TLS version 1.2