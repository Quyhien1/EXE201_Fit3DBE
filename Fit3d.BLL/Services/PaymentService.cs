using Fit3d.BLL.Common;
using Fit3d.BLL.Configuration;
using Fit3d.BLL.DTOs;
using Fit3d.BLL.Interfaces;
using Fit3d.BLL.Utilities;
using FIt3d.DAL.Entities;
using FIt3d.DAL.Enums;
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
                    returnUrl: _payOsSetings.OrderReturnUrl,
                    cancelUrl: _payOsSetings.OrderCancelUrl,
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment link for OrderId: {OrderId}", requestBody.OrderId);
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

            if (transaction.TransactionStatus == TransactionStatus.Return)
            {
                return new ServiceResponse { Succeeded = true, Message = "Giao dịch đã được xác nhận trước đó." };
            }

            if (transaction.TransactionStatus != TransactionStatus.Pending)
            {
                _logger.LogError("Transaction status not valid with {OrderId}", orderId);
                return new ServiceResponse { Succeeded = false, Message = "Trạng thái giao dịch không hợp lệ!" };
            }

            try
            {
                if (!transaction.OrderId.HasValue)
                {
                    _logger.LogError("Order transaction missing OrderId: {TransactionId}", transaction.Id);
                    return new ServiceResponse { Succeeded = false, Message = "Giao dịch không gắn với đơn hàng!" };
                }

                var orderResponse = await _orderService.UpdateOrderToReturn(transaction.OrderId.Value, cancellationToken);
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
            catch (Exception ex)
            {
                transaction.TransactionStatus = TransactionStatus.Fail;
                _unitOfWork.GetRepository<PaymentTransaction>().UpdateAsync(transaction);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogError(ex, "Payment return fail with OrderId: {OrderId}", transaction.OrderId);
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

            if (transaction.TransactionStatus == TransactionStatus.Cancel || transaction.TransactionStatus == TransactionStatus.Fail)
            {
                return new ServiceResponse { Succeeded = true, Message = "Giao dịch đã được hủy trước đó." };
            }

            if (transaction.TransactionStatus != TransactionStatus.Pending)
            {
                _logger.LogError("Transaction status not valid with {OrderId}", orderId);
                return new ServiceResponse { Succeeded = false, Message = "Trạng thái giao dịch không hợp lệ!" };
            }

            try
            {
                if (!transaction.OrderId.HasValue)
                {
                    _logger.LogError("Order transaction missing OrderId: {TransactionId}", transaction.Id);
                    return new ServiceResponse { Succeeded = false, Message = "Giao dịch không gắn với đơn hàng!" };
                }

                var orderResponse = await _orderService.UpdateOrderToCancel(transaction.OrderId.Value, cancellationToken);
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
            catch (Exception ex)
            {
                transaction.TransactionStatus = TransactionStatus.Fail;
                _unitOfWork.GetRepository<PaymentTransaction>().UpdateAsync(transaction);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogError(ex, "Payment cancel fail with OrderId: {OrderId}", transaction.OrderId);
                return new ServiceResponse { Succeeded = false, Message = "Giao dịch thất bại!" };
            }
        }

        public async Task<ServiceResponse> GetSubscriptionPlansByType(PlanType planType, CancellationToken cancellationToken = default)
        {
            try
            {
                var plans = await _unitOfWork.GetRepository<SubscriptionPlan>()
                    .GetListAsync(predicate: p => p.PlanType == planType && p.IsActive);

                var result = plans.Select(p => new SubscriptionPlanResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    PlanType = p.PlanType,
                    Price = p.Price,
                    DurationInDays = p.DurationInDays,
                    MaxModels = p.MaxModels,
                    MaxEditsPerModel = p.MaxEditsPerModel,
                    MaxAIRequestsPerMonth = p.MaxAIRequestsPerMonth,
                    HasAIFeature = p.HasAIFeature,
                }).ToList();

                return new ResponseData<List<SubscriptionPlanResponse>>
                {
                    Succeeded = true,
                    Message = "Lấy danh sách gói thành công!",
                    Data = result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting subscription plans for PlanType: {PlanType}", planType);
                return new ServiceResponse { Succeeded = false, Message = "Lỗi khi lấy danh sách gói subscription!" };
            }
        }

        public async Task<ServiceResponse> CreateSubscriptionPayment(SubscriptionPaymentRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var plan = await _unitOfWork.GetRepository<SubscriptionPlan>()
                    .SingleOrDefaultAsync(predicate: p => p.Id == request.SubscriptionPlanId && p.IsActive);

                if (plan == null)
                {
                    _logger.LogError("Subscription plan not found: {PlanId}", request.SubscriptionPlanId);
                    return new ServiceResponse { Succeeded = false, Message = "Không tìm thấy gói subscription!" };
                }

                if (request.PlanType != plan.PlanType)
                {
                    _logger.LogWarning(
                        "PlanType mismatch for subscription payment. Request sent {Requested}, but plan {PlanId} is {Actual}. Using plan type from database.",
                        request.PlanType,
                        plan.Id,
                        plan.PlanType);
                }

                var user = await _unitOfWork.GetRepository<User>().GetByIdAsync(request.UserId);
                if (user == null || user.IsDeleted)
                {
                    _logger.LogError("User not found: {UserId}", request.UserId);
                    return new ServiceResponse { Succeeded = false, Message = "Không tìm thấy người dùng!" };
                }

                if (!IsStarterShopPlan(plan) &&
                    (!string.IsNullOrWhiteSpace(request.ShopName) || !string.IsNullOrWhiteSpace(request.ShopDescription)))
                {
                    return new ServiceResponse
                    {
                        Succeeded = false,
                        Message = "Chỉ gói Starter Pack mới được phép gửi thông tin shop."
                    };
                }

                var subscription = new Subscription
                {
                    UserId = request.UserId,
                    SubscriptionPlanId = plan.Id,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(plan.DurationInDays),
                    Status = SubscriptionStatus.Pending,
                    PaidAmount = plan.Price,
                    PaymentMethod = "PayOS"
                };
                await _unitOfWork.GetRepository<Subscription>().InsertAsync(subscription);
                await _unitOfWork.SaveChangesAsync();

                var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var description = plan.Name.Length > 25 ? plan.Name[..25] : plan.Name;
                var item = new ItemData(plan.Name, 1, (int)plan.Price);
                var items = new List<ItemData> { item };
                var expiredAt = TimeConverter.GetCurrentVietNamTime()
                    .AddSeconds(_payOsSetings.ExpirationSeconds)
                    .ToUnixTimeSeconds();

                var data = new PaymentData(
                    orderCode: orderCode,
                    amount: item.price,
                    description: description,
                    items: items,
                    returnUrl: _payOsSetings.SubscriptionReturnUrl,
                    cancelUrl: _payOsSetings.SubscriptionCancelUrl,
                    expiredAt: expiredAt
                );
                var response = await _payOs.createPaymentLink(data);

                subscription.PaymentTransactionId = response.paymentLinkId;
                _unitOfWork.GetRepository<Subscription>().UpdateAsync(subscription);

                var subscriptionTransaction = new PaymentTransaction
                {
                    OrderId = null,
                    SubscriptionId = subscription.Id,
                    UserId = request.UserId,
                    OrderCode = orderCode,
                    PaymentLinkId = response.paymentLinkId,
                    CheckoutUrl = response.checkoutUrl,
                    QrCode = response.qrCode,
                    Amount = plan.Price,
                    Description = $"Thanh toán gói {plan.Name}",
                    TransactionStatus = TransactionStatus.Pending,
                    PaymentMethod = PaymentMethod.PayOs,
                };
                await _unitOfWork.GetRepository<PaymentTransaction>().InsertAsync(subscriptionTransaction);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Subscription payment created for UserId: {UserId}, PlanId: {PlanId}", request.UserId, plan.Id);
                return new ResponseData<CreatePaymentResult>
                {
                    Succeeded = true,
                    Message = "Tạo liên kết thanh toán gói subscription thành công!",
                    Data = response
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating subscription payment for UserId: {UserId}", request.UserId);
                return new ServiceResponse { Succeeded = false, Message = "Lỗi khi tạo liên kết thanh toán subscription!" };
            }
        }

        public async Task<ServiceResponse> SubscriptionPaymentReturn(Guid subscriptionId, CancellationToken cancellationToken = default)
        {
            var subscription = await _unitOfWork.GetRepository<Subscription>()
                .SingleOrDefaultAsync(
                    predicate: s => s.Id == subscriptionId,
                    include: x => x
                        .Include(s => s.SubscriptionPlan)
                        .Include(s => s.User)
                );

            if (subscription == null)
            {
                _logger.LogError("Subscription not found: {SubscriptionId}", subscriptionId);
                return new ServiceResponse { Succeeded = false, Message = "Không tìm thấy gói subscription!" };
            }

            if (subscription.Status == SubscriptionStatus.Active)
            {
                return new ServiceResponse { Succeeded = true, Message = "Subscription đã được kích hoạt trước đó." };
            }

            if (subscription.Status != SubscriptionStatus.Pending)
            {
                _logger.LogError("Subscription status not valid: {SubscriptionId}", subscriptionId);
                return new ServiceResponse { Succeeded = false, Message = "Trạng thái subscription không hợp lệ!" };
            }

            try
            {
                var subscriptionTransaction = await _unitOfWork.GetRepository<PaymentTransaction>()
                    .SingleOrDefaultAsync(predicate: t =>
                        t.SubscriptionId == subscription.Id ||
                        (!string.IsNullOrWhiteSpace(subscription.PaymentTransactionId) && t.PaymentLinkId == subscription.PaymentTransactionId));

                var plan = subscription.SubscriptionPlan;
                var durationInDays = plan.DurationInDays;
                subscription.SubscriptionPlan = null!;
                subscription.Status = SubscriptionStatus.Active;
                subscription.StartDate = DateTime.UtcNow;
                subscription.EndDate = DateTime.UtcNow.AddDays(durationInDays);
                subscription.UpdatedAt = DateTime.UtcNow;

                if (subscription.User != null &&
                    plan.PlanType == PlanType.B2B_Shop)
                {
                    subscription.User.Role = UserRole.Shop;
                    subscription.User.UpdatedAt = DateTime.UtcNow;
                    _unitOfWork.GetRepository<User>().UpdateAsync(subscription.User);
                }

                if (subscriptionTransaction != null)
                {
                    subscriptionTransaction.TransactionStatus = TransactionStatus.Return;
                    _unitOfWork.GetRepository<PaymentTransaction>().UpdateAsync(subscriptionTransaction);
                }

                _unitOfWork.GetRepository<Subscription>().UpdateAsync(subscription);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Subscription payment return successfully: {SubscriptionId}", subscriptionId);
                return new ServiceResponse { Succeeded = true, Message = "Thanh toán gói subscription thành công!" };
            }
            catch (Exception ex)
            {
                var subscriptionTransaction = await _unitOfWork.GetRepository<PaymentTransaction>()
                    .SingleOrDefaultAsync(predicate: t =>
                        t.SubscriptionId == subscription.Id ||
                        (!string.IsNullOrWhiteSpace(subscription.PaymentTransactionId) && t.PaymentLinkId == subscription.PaymentTransactionId));

                subscription.SubscriptionPlan = null!;
                subscription.Status = SubscriptionStatus.Cancelled;
                subscription.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.GetRepository<Subscription>().UpdateAsync(subscription);

                if (subscriptionTransaction != null)
                {
                    subscriptionTransaction.TransactionStatus = TransactionStatus.Fail;
                    _unitOfWork.GetRepository<PaymentTransaction>().UpdateAsync(subscriptionTransaction);
                }

                await _unitOfWork.SaveChangesAsync();
                _logger.LogError(ex, "Subscription payment return fail: {SubscriptionId}", subscriptionId);
                return new ServiceResponse { Succeeded = false, Message = "Thanh toán gói subscription thất bại!" };
            }
        }

        public async Task<ServiceResponse> SubscriptionPaymentCancel(Guid subscriptionId, CancellationToken cancellationToken = default)
        {
            var subscription = await _unitOfWork.GetRepository<Subscription>()
                .SingleOrDefaultAsync(predicate: s => s.Id == subscriptionId);

            if (subscription == null)
            {
                _logger.LogError("Subscription not found: {SubscriptionId}", subscriptionId);
                return new ServiceResponse { Succeeded = false, Message = "Không tìm thấy gói subscription!" };
            }

            if (subscription.Status == SubscriptionStatus.Cancelled)
            {
                return new ServiceResponse { Succeeded = true, Message = "Subscription đã được hủy trước đó." };
            }

            if (subscription.Status != SubscriptionStatus.Pending)
            {
                _logger.LogError("Subscription status not valid: {SubscriptionId}", subscriptionId);
                return new ServiceResponse { Succeeded = false, Message = "Trạng thái subscription không hợp lệ!" };
            }

            try
            {
                var subscriptionTransaction = await _unitOfWork.GetRepository<PaymentTransaction>()
                    .SingleOrDefaultAsync(predicate: t =>
                        t.SubscriptionId == subscription.Id ||
                        (!string.IsNullOrWhiteSpace(subscription.PaymentTransactionId) && t.PaymentLinkId == subscription.PaymentTransactionId));

                subscription.Status = SubscriptionStatus.Cancelled;
                subscription.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.GetRepository<Subscription>().UpdateAsync(subscription);

                if (subscriptionTransaction != null)
                {
                    subscriptionTransaction.TransactionStatus = TransactionStatus.Cancel;
                    _unitOfWork.GetRepository<PaymentTransaction>().UpdateAsync(subscriptionTransaction);
                }

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Subscription payment cancelled: {SubscriptionId}", subscriptionId);
                return new ServiceResponse { Succeeded = true, Message = "Hủy thanh toán gói subscription thành công!" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling subscription payment: {SubscriptionId}", subscriptionId);
                return new ServiceResponse { Succeeded = false, Message = "Lỗi khi hủy thanh toán subscription!" };
            }
        }

        private static bool IsStarterShopPlan(SubscriptionPlan plan)
        {
            if (plan.PlanType != PlanType.B2B_Shop)
            {
                return false;
            }

            return (plan.Name?.Trim().Contains("starter", StringComparison.OrdinalIgnoreCase)).GetValueOrDefault();
        }

        public async Task<ResponseData<PaymentTrackingResponse>> GetPaymentTrackingStatus(
            long? orderCode,
            string? paymentLinkId,
            CancellationToken cancellationToken = default)
        {
            var tracking = await FindPaymentTracking(orderCode, paymentLinkId, cancellationToken);
            if (tracking == null)
            {
                return new ResponseData<PaymentTrackingResponse>
                {
                    Succeeded = false,
                    Message = "Không tìm thấy giao dịch thanh toán.",
                    Data = null
                };
            }

            return new ResponseData<PaymentTrackingResponse>
            {
                Succeeded = true,
                Message = tracking.Message,
                Data = tracking
            };
        }

        public async Task<ResponseData<PaymentTrackingResponse>> ReconcilePaymentCallback(
            PaymentCallbackSyncRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!request.OrderCode.HasValue && string.IsNullOrWhiteSpace(request.PaymentLinkId))
            {
                return new ResponseData<PaymentTrackingResponse>
                {
                    Succeeded = false,
                    Message = "Thiếu thông tin callback thanh toán.",
                    Data = null
                };
            }

            var normalizedStatus = request.Status?.Trim().ToUpperInvariant();
            if (request.Cancel || string.Equals(normalizedStatus, "CANCELLED", StringComparison.OrdinalIgnoreCase))
            {
                var target = await FindMatchingPaymentTarget(request.OrderCode, request.PaymentLinkId, cancellationToken);
                if (target.Transaction != null)
                {
                    if (target.Transaction.OrderId.HasValue)
                    {
                        await PaymentCancel(target.Transaction.OrderId.Value, cancellationToken);
                    }
                }
                else if (target.Subscription != null)
                {
                    await SubscriptionPaymentCancel(target.Subscription.Id, cancellationToken);
                }
            }

            return await GetPaymentTrackingStatus(request.OrderCode, request.PaymentLinkId, cancellationToken);
        }

        public async Task<ServiceResponse> HandlePaymentWebhook(
            WebhookType webhook,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var paymentData = _payOs.verifyPaymentWebhookData(webhook);
                var target = await FindMatchingPaymentTarget(paymentData.orderCode, paymentData.paymentLinkId, cancellationToken);
                var isSuccessfulPayment =
                    webhook.success &&
                    string.Equals(webhook.code, "00", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(paymentData.code, "00", StringComparison.OrdinalIgnoreCase);

                if (target.Transaction == null && target.Subscription == null)
                {
                    _logger.LogInformation(
                        "Webhook verified but no matching local payment was found. OrderCode: {OrderCode}, PaymentLinkId: {PaymentLinkId}",
                        paymentData.orderCode,
                        paymentData.paymentLinkId);
                    return new ServiceResponse
                    {
                        Succeeded = true,
                        Message = "Webhook hợp lệ nhưng không khớp giao dịch nội bộ."
                    };
                }

                if (target.Transaction != null)
                {
                    if (!target.Transaction.OrderId.HasValue)
                    {
                        return new ServiceResponse
                        {
                            Succeeded = false,
                            Message = "Giao dịch order không hợp lệ."
                        };
                    }

                    return isSuccessfulPayment
                        ? await PaymentReturn(target.Transaction.OrderId.Value, cancellationToken)
                        : await PaymentCancel(target.Transaction.OrderId.Value, cancellationToken);
                }

                if (target.Subscription != null)
                {
                    return isSuccessfulPayment
                        ? await SubscriptionPaymentReturn(target.Subscription.Id, cancellationToken)
                        : await SubscriptionPaymentCancel(target.Subscription.Id, cancellationToken);
                }

                return new ServiceResponse
                {
                    Succeeded = false,
                    Message = "Không thể xử lý webhook thanh toán."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook verification failed.");
                return new ServiceResponse
                {
                    Succeeded = false,
                    Message = "Webhook không hợp lệ."
                };
            }
        }

        public Task<ServiceResponse> ConfirmWebhook(string webhookUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                return Task.FromResult(new ServiceResponse
                {
                    Succeeded = false,
                    Message = "Webhook URL không được để trống."
                });
            }

            try
            {
                _payOs.confirmWebhook(webhookUrl.Trim());
                return Task.FromResult(new ServiceResponse
                {
                    Succeeded = true,
                    Message = "Xác nhận webhook thành công!"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming webhook URL: {WebhookUrl}", webhookUrl);
                return Task.FromResult(new ServiceResponse
                {
                    Succeeded = false,
                    Message = "Không thể xác nhận webhook URL."
                });
            }
        }

        private async Task<PaymentTrackingResponse?> FindPaymentTracking(
            long? orderCode,
            string? paymentLinkId,
            CancellationToken cancellationToken)
        {
            var target = await FindMatchingPaymentTarget(orderCode, paymentLinkId, cancellationToken);
            if (target.Transaction != null)
            {
                return MapOrderTracking(target.Transaction);
            }

            if (target.Subscription != null)
            {
                return MapSubscriptionTracking(target.Subscription);
            }

            return null;
        }

        private async Task<(PaymentTransaction? Transaction, Subscription? Subscription)> FindMatchingPaymentTarget(
            long? orderCode,
            string? paymentLinkId,
            CancellationToken cancellationToken)
        {
            PaymentTransaction? transaction = null;
            Subscription? subscription = null;

            if (orderCode.HasValue)
            {
                transaction = await _unitOfWork.GetRepository<PaymentTransaction>()
                    .SingleOrDefaultAsync(
                        predicate: t => t.OrderId.HasValue && t.OrderCode == orderCode.Value,
                        include: x => x.Include(t => t.Order));
            }

            if (transaction == null && !string.IsNullOrWhiteSpace(paymentLinkId))
            {
                transaction = await _unitOfWork.GetRepository<PaymentTransaction>()
                    .SingleOrDefaultAsync(
                        predicate: t => t.OrderId.HasValue && t.PaymentLinkId == paymentLinkId,
                        include: x => x.Include(t => t.Order));
            }

            if (transaction == null && !string.IsNullOrWhiteSpace(paymentLinkId))
            {
                subscription = await _unitOfWork.GetRepository<Subscription>()
                    .SingleOrDefaultAsync(
                        predicate: s => s.PaymentTransactionId == paymentLinkId,
                        include: x => x.Include(s => s.SubscriptionPlan).Include(s => s.User));
            }

            if (subscription == null && !string.IsNullOrWhiteSpace(paymentLinkId))
            {
                var subscriptionTransaction = await _unitOfWork.GetRepository<PaymentTransaction>()
                    .SingleOrDefaultAsync(predicate: t => t.SubscriptionId.HasValue && t.PaymentLinkId == paymentLinkId);

                if (subscriptionTransaction?.SubscriptionId.HasValue == true)
                {
                    var subscriptionId = subscriptionTransaction.SubscriptionId.Value;
                    subscription = await _unitOfWork.GetRepository<Subscription>()
                        .SingleOrDefaultAsync(
                            predicate: s => s.Id == subscriptionId,
                            include: x => x.Include(s => s.SubscriptionPlan).Include(s => s.User));
                }
            }

            return (transaction, subscription);
        }

        private static PaymentTrackingResponse MapOrderTracking(PaymentTransaction transaction)
        {
            var status = transaction.TransactionStatus switch
            {
                TransactionStatus.Return => "PAID",
                TransactionStatus.Cancel => "CANCELLED",
                TransactionStatus.Fail => "FAILED",
                _ => "PENDING"
            };

            return new PaymentTrackingResponse
            {
                Success = status == "PAID",
                IsFinal = status is "PAID" or "CANCELLED" or "FAILED",
                Status = status,
                Type = "order",
                OrderId = transaction.OrderId,
                OrderCode = transaction.OrderCode,
                PaymentLinkId = transaction.PaymentLinkId,
                Amount = transaction.Amount,
                Message = status switch
                {
                    "PAID" => "Đơn hàng đã được xác nhận thanh toán.",
                    "CANCELLED" => "Đơn hàng đã bị hủy thanh toán.",
                    "FAILED" => "Thanh toán đơn hàng thất bại.",
                    _ => "Đang chờ payOS xác nhận thanh toán qua webhook."
                }
            };
        }

        private static PaymentTrackingResponse MapSubscriptionTracking(Subscription subscription)
        {
            var status = subscription.Status switch
            {
                SubscriptionStatus.Active => "PAID",
                SubscriptionStatus.Cancelled => "CANCELLED",
                _ => "PENDING"
            };

            return new PaymentTrackingResponse
            {
                Success = status == "PAID",
                IsFinal = status is "PAID" or "CANCELLED",
                Status = status,
                Type = "subscription",
                SubscriptionId = subscription.Id,
                PaymentLinkId = subscription.PaymentTransactionId,
                Amount = subscription.PaidAmount,
                Message = status switch
                {
                    "PAID" => "Gói subscription đã được kích hoạt.",
                    "CANCELLED" => "Thanh toán subscription đã bị hủy.",
                    _ => "Đang chờ payOS xác nhận subscription qua webhook."
                }
            };
        }
    }
}
