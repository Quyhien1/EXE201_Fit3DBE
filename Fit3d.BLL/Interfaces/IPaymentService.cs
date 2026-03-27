using Fit3d.BLL.Common;
using Fit3d.BLL.DTOs;
using FIt3d.DAL.Enums;
using Net.payOS.Types;

namespace Fit3d.BLL.Interfaces
{
    public interface IPaymentService
    {
        Task<ServiceResponse> CreatePayment(PaymentRequest requestBody, CancellationToken cancellationToken = default);
        Task<ServiceResponse> PaymentReturn(Guid orderId, CancellationToken cancellationToken = default);
        Task<ServiceResponse> PaymentCancel(Guid orderId, CancellationToken cancellationToken = default);

        Task<ServiceResponse> GetSubscriptionPlansByType(PlanType planType, CancellationToken cancellationToken = default);
        Task<ServiceResponse> CreateSubscriptionPayment(SubscriptionPaymentRequest request, CancellationToken cancellationToken = default);
        Task<ServiceResponse> SubscriptionPaymentReturn(Guid subscriptionId, CancellationToken cancellationToken = default);
        Task<ServiceResponse> SubscriptionPaymentCancel(Guid subscriptionId, CancellationToken cancellationToken = default);
        Task<ResponseData<PaymentTrackingResponse>> GetPaymentTrackingStatus(long? orderCode, string? paymentLinkId, CancellationToken cancellationToken = default);
        Task<ResponseData<PaymentTrackingResponse>> ReconcilePaymentCallback(PaymentCallbackSyncRequest request, CancellationToken cancellationToken = default);
        Task<ServiceResponse> HandlePaymentWebhook(WebhookType webhook, CancellationToken cancellationToken = default);
        Task<ServiceResponse> ConfirmWebhook(string webhookUrl, CancellationToken cancellationToken = default);
    }
}
