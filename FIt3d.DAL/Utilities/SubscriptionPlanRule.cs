using FIt3d.DAL.Entities;
using FIt3d.DAL.Enums;

namespace FIt3d.DAL.Utilities
{
    public static class SubscriptionPlanRule
    {
        public const string StarterKeyword = "starter";

        public static bool IsStarterShopPlan(SubscriptionPlan plan)
        {
            return IsStarterShopPlan(plan.PlanType, plan.Name);
        }

        public static bool IsStarterShopPlan(PlanType planType, string? planName)
        {
            if (planType != PlanType.B2B_Shop)
            {
                return false;
            }

            return planName?.Trim().Contains(StarterKeyword, StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
