using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace EventGridWebhookApp.Extensions;

public static class ConfigurationExtensions
{
    public static WebApplicationBuilder AddKeyVaultConfiguration(this WebApplicationBuilder builder)
    {
        // Configure Key Vault integration if not in development
        if (!builder.Environment.IsDevelopment())
        {
            var keyVaultName = builder.Configuration["KeyVaultName"];
            if (!string.IsNullOrEmpty(keyVaultName))
            {
                var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
                var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ManagedIdentityClientId = builder.Configuration["ManagedIdentityClientId"],
                    ExcludeSharedTokenCacheCredential = true
                });

                // Register SecretClient for dependency injection
                builder.Services.AddSingleton(new SecretClient(keyVaultUri, credential));

                // Add Azure Key Vault as a configuration provider
                builder.Configuration.AddAzureKeyVault(keyVaultUri, credential);
            }
        }
        return builder;
    }
}
