using Fit3d.BLL.Common;
using Fit3d.BLL.Configuration;
using Fit3d.BLL.DTOs;
using Fit3d.BLL.Interfaces;
using Fit3d.BLL.Utilities;
using FIt3d.DAL.Entities;
using FIt3d.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Net.payOS;
using Net.payOS.Types;
using PaymentTransaction = FIt3d.DAL.Entities.Transaction;

namespace Fit3d.BLL.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly PayOS _payOs;
        private readonly IUnitOfWork _unitOfWork;
        private readonly PayOsSetings _payOsSetings;
        private readonly IOrderService _orderService;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(IUnitOfWork unitOfWork, ILogger<PaymentService> logger, IOptions<PayOsSetings> payOsSetings, IOrderService orderService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _orderService = orderService;
            _payOsSetings = payOsSetings.Value;
            _payOs = new PayOS(_payOsSetings!.ClientId, _payOsSetings.ApiKey, _payOsSetings.ChecksumKey);
        }

        public async Task<ServiceResponse> CreatePayment(PaymentRequest requestBody, CancellationToken cancellationToken = default)
        {
            try
            {
                var order = await _unitOfWork.GetRepository<Order>()
                    .SingleOrDefaultAsync(
                        predicate: o => o.Id == requestBody.OrderId && !o.IsDeleted,
                        include: x => x.Include(o => o.OrderItems).ThenInclude(oi => oi.Product)
                    );

                if (order == null)
                {
                    _logger.LogError("Order not found with OrderId: {OrderId}", requestBody.OrderId);
                    return new ServiceResponse { Succeeded = false, Message = "Không tìm thấy đơn hàng!" };
                }

                if (order.CreatedAt < DateTime.UtcNow.AddMinutes(-20))
                {
                    _logger.LogError("Order expired with OrderId: {OrderId}", requestBody.OrderId);
                    order.PaymentStatus = PaymentStatus.Failed;
                    _unitOfWork.GetRepository<Order>().UpdateAsync(order);
                    await _unitOfWork.SaveChangesAsync();
                    return new ServiceResponse { Succeeded = false, Message = "Đơn hàng đã hết hạn! Vui lòng mua đơn hàng mới." };
                }

                if (order.PaymentStatus != PaymentStatus.Pending)
                {
                    _logger.LogError("Order status is not valid with OrderId: {OrderId}", requestBody.OrderId);
                    return new ServiceResponse { Succeeded = false, Message = "Trạng thái đơn hàng không hợp lệ!" };
                }

                var itemName = order.OrderItems.FirstOrDefault()?.Product?.Name ?? order.OrderNumber;
                var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var item = new ItemData(itemName, 1, (int)order.TotalAmount);
                var items = new List<ItemData> { item };
                var expiredAt = TimeConverter.GetCurrentVietNamTime()
                    .AddSeconds(_payOsSetings.ExpirationSeconds)
                    .ToUnixTimeSeconds();

                var data = new PaymentData(
                    orderCode: orderCode,
                    amount: item.price,
                    description: "Thanh toán đơn hàng",
                    items: items,
                    returnUrl: $"{_payOsSetings.BaseUrl}/payments/return?orderId={order.Id}",
                    cancelUrl: $"{_payOsSetings.BaseUrl}/payments/cancel?orderId={order.Id}",
                    expiredAt: expiredAt
                );
                var response = await _payOs.createPaymentLink(data);

                var transaction = new PaymentTransaction
                {
                    OrderId = order.Id,
                    UserId = order.UserId,
                    OrderCode = orderCode,
                    PaymentLinkId = response.paymentLinkId,
                    CheckoutUrl = response.checkoutUrl,
                    QrCode = response.qrCode,
                    Amount = order.TotalAmount,
                    Description = data.description,
                    TransactionStatus = TransactionStatus.Pending,
                    PaymentMethod = PaymentMethod.PayOs,
                };
                await _unitOfWork.GetRepository<PaymentTransaction>().InsertAsync(transaction);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Transaction created successfully with OrderId: {OrderId}", transaction.OrderId);
                return new ResponseData<CreatePaymentResult>
                {
                    Succeeded = true,
                    Message = "Tạo liên kết thanh toán thành công!",
                    Data = response
                };
            }
            catch
            {
                _logger.LogError("Error creating payment link for OrderId: {OrderId}", requestBody.OrderId);
                return new ServiceResponse { Succeeded = false, Message = "Lỗi khi tạo liên kết thanh toán!" };
            }
        }

        public async Task<ServiceResponse> PaymentReturn(Guid orderId, CancellationToken cancellationToken = default)
        {
            var transaction = await _unitOfWork.GetRepository<PaymentTransaction>()
                .SingleOrDefaultAsync(predicate: t => t.OrderId == orderId);

            if (transaction == null)
            {
                _logger.LogError("Order not found with OrderId: {OrderId}", orderId);
                return new ServiceResponse { Succeeded = false, Message = "Không tìm thấy đơn hàng!" };
            }

            if (transaction.TransactionStatus != TransactionStatus.Pending)
            {
                _logger.LogError("Transaction status not valid with {OrderId}", orderId);
                return new ServiceResponse { Succeeded = false, Message = "Trạng thái giao dịch không hợp lệ!" };
            }

            try
            {
                var orderResponse = await _orderService.UpdateOrderToReturn(transaction.OrderId, cancellationToken);
                if (!orderResponse.Succeeded)
                {
                    transaction.TransactionStatus = TransactionStatus.Fail;
                    _unitOfWork.GetRepository<PaymentTransaction>().UpdateAsync(transaction);
                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogInformation("Payment return fail with OrderId: {OrderId}", transaction.OrderId);
                    return new ServiceResponse { Succeeded = false, Message = "Giao dịch thất bại!" };
                }

                transaction.TransactionStatus = TransactionStatus.Return;
                _unitOfWork.GetRepository<PaymentTransaction>().UpdateAsync(transaction);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Payment return successfully with OrderId: {OrderId}", transaction.OrderId);
                return new ServiceResponse { Succeeded = true, Message = "Giao dịch thành công!" };
            }
            catch
            {
                transaction.TransactionStatus = TransactionStatus.Fail;
                _unitOfWork.GetRepository<PaymentTransaction>().UpdateAsync(transaction);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Payment return fail with OrderId: {OrderId}", transaction.OrderId);
                return new ServiceResponse { Succeeded = false, Message = "Giao dịch thất bại!" };
            }
        }

        public async Task<ServiceResponse> PaymentCancel(Guid orderId, CancellationToken cancellationToken = default)
        {
            var transaction = await _unitOfWork.GetRepository<PaymentTransaction>()
                .SingleOrDefaultAsync(predicate: t => t.OrderId == orderId);

            if (transaction == null)
            {
                _logger.LogError("Order not found with OrderId: {OrderId}", orderId);
                return new ServiceResponse { Succeeded = false, Message = "Không tìm thấy đơn hàng!" };
            }

            if (transaction.TransactionStatus != TransactionStatus.Pending)
            {
                _logger.LogError("Transaction status not valid with {OrderId}", orderId);
                return new ServiceResponse { Succeeded = false, Message = "Trạng thái giao dịch không hợp lệ!" };
            }

            try
            {
                var orderResponse = await _orderService.UpdateOrderToCancel(transaction.OrderId, cancellationToken);
                if (!orderResponse.Succeeded)
                {
                    transaction.TransactionStatus = TransactionStatus.Fail;
                    _unitOfWork.GetRepository<PaymentTransaction>().UpdateAsync(transaction);
                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogInformation("Payment cancel fail with OrderId: {OrderId}", transaction.OrderId);
                    return new ServiceResponse { Succeeded = false, Message = "Giao dịch thất bại!" };
                }

                transaction.TransactionStatus = TransactionStatus.Cancel;
                _unitOfWork.GetRepository<PaymentTransaction>().UpdateAsync(transaction);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Payment cancel successfully with OrderId: {OrderId}", transaction.OrderId);
                return new ServiceResponse { Succeeded = true, Message = "Hủy giao dịch thành công!" };
            }
            catch
            {
                transaction.TransactionStatus = TransactionStatus.Fail;
                _unitOfWork.GetRepository<PaymentTransaction>().UpdateAsync(transaction);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Payment cancel fail with OrderId: {OrderId}", transaction.OrderId);
                return new ServiceResponse { Succeeded = false, Message = "Giao dịch thất bại!" };
            }
        }
    }
}
