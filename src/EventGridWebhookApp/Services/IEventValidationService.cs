using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace EventGridWebhookApp.Services
{
    public interface IEventValidationService
    {
        Task<bool> ValidateSignature(string signatureHeader, string requestBody);
        bool IsValidTopic(string topic);
        Task<bool> HandleSubscriptionValidationEvent(string requestBody);
    }
}
