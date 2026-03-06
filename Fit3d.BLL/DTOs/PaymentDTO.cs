namespace Fit3d.BLL.DTOs
{
    public class PaymentRequest
    {
        public Guid OrderId { get; set; }
    }

    public class SubscriptionPaymentRequest
    {
        public Guid UserId { get; set; }
        public Guid SubscriptionPlanId { get; set; }
    }
}
