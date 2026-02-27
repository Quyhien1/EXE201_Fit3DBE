using Fit3d.BLL.Common;
using Fit3d.BLL.DTOs;
using Fit3d.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

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
    }
}
