using Fit3d.BLL.Common;
using Fit3d.BLL.DTOs;
using Fit3d.BLL.Interfaces;
using FIt3d.DAL.Enums;
using Microsoft.AspNetCore.Mvc;
using Net.payOS.Types;

namespace Fit3d.API.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost("create")]
        [ProducesResponseType(typeof(ResponseData<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreatePayment([FromBody] PaymentRequest request, CancellationToken cancellationToken)
        {
            var result = await _paymentService.CreatePayment(request, cancellationToken);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("return")]
        [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> PaymentReturn([FromQuery] Guid orderId, CancellationToken cancellationToken)
        {
            var result = await _paymentService.PaymentReturn(orderId, cancellationToken);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("cancel")]
        [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> PaymentCancel([FromQuery] Guid orderId, CancellationToken cancellationToken)
        {
            var result = await _paymentService.PaymentCancel(orderId, cancellationToken);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("status")]
        [ProducesResponseType(typeof(ResponseData<PaymentTrackingResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetPaymentStatus(
            [FromQuery] long? orderCode,
            [FromQuery] string? paymentLinkId,
            CancellationToken cancellationToken)
        {
            var result = await _paymentService.GetPaymentTrackingStatus(orderCode, paymentLinkId, cancellationToken);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("callback/reconcile")]
        [ProducesResponseType(typeof(ResponseData<PaymentTrackingResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ReconcilePaymentCallback(
            [FromBody] PaymentCallbackSyncRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _paymentService.ReconcilePaymentCallback(request, cancellationToken);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("subscription/plans")]
        [ProducesResponseType(typeof(ResponseData<List<SubscriptionPlanResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSubscriptionPlans([FromQuery] PlanType planType, CancellationToken cancellationToken)
        {
            var result = await _paymentService.GetSubscriptionPlansByType(planType, cancellationToken);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("subscription/create")]
        [ProducesResponseType(typeof(ResponseData<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateSubscriptionPayment([FromBody] SubscriptionPaymentRequest request, CancellationToken cancellationToken)
        {
            var result = await _paymentService.CreateSubscriptionPayment(request, cancellationToken);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("subscription/return")]
        [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SubscriptionPaymentReturn([FromQuery] Guid subscriptionId, CancellationToken cancellationToken)
        {
            var result = await _paymentService.SubscriptionPaymentReturn(subscriptionId, cancellationToken);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpGet("subscription/cancel")]
        [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SubscriptionPaymentCancel([FromQuery] Guid subscriptionId, CancellationToken cancellationToken)
        {
            var result = await _paymentService.SubscriptionPaymentCancel(subscriptionId, cancellationToken);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("webhook")]
        [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> HandleWebhook([FromBody] WebhookType webhook, CancellationToken cancellationToken)
        {
            var result = await _paymentService.HandlePaymentWebhook(webhook, cancellationToken);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [HttpPost("webhook/confirm")]
        [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConfirmWebhook([FromBody] ConfirmWebhookRequest request, CancellationToken cancellationToken)
        {
            var result = await _paymentService.ConfirmWebhook(request.WebhookUrl, cancellationToken);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }
    }
}
