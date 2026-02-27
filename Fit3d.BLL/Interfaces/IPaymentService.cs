using Fit3d.BLL.Common;
using Fit3d.BLL.DTOs;

namespace Fit3d.BLL.Interfaces
{
    public interface IPaymentService
    {
        Task<ServiceResponse> CreatePayment(PaymentRequest requestBody, CancellationToken cancellationToken = default);
        Task<ServiceResponse> PaymentReturn(Guid orderId, CancellationToken cancellationToken = default);
        Task<ServiceResponse> PaymentCancel(Guid orderId, CancellationToken cancellationToken = default);
    }
}
