using Alpha.API.Constants;

namespace Alpha.API.Services;

public class OrderWorkflowService
{
    private static readonly Dictionary<string, string[]> AllowedTransitions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [OrderStatuses.PaymentPending] =
                new[]
                {
                    OrderStatuses.PaymentPaid,
                    OrderStatuses.PaymentFailed,
                    OrderStatuses.CustomerCancelled
                },

            [OrderStatuses.PaymentPaid] =
                new[]
                {
                    OrderStatuses.WaitingForSupplier
                },

            [OrderStatuses.WaitingForSupplier] =
                new[]
                {
                    OrderStatuses.SupplierAssigned,
                    OrderStatuses.SupplierUnavailable,
                    OrderStatuses.CustomerCancelled
                },

            [OrderStatuses.SupplierAssigned] =
                new[]
                {
                    OrderStatuses.SupplierAccepted,
                    OrderStatuses.SupplierDeclined
                },

            [OrderStatuses.SupplierAccepted] =
                new[]
                {
                    OrderStatuses.WaitingForDriver
                },

            [OrderStatuses.WaitingForDriver] =
                new[]
                {
                    OrderStatuses.DriverAssigned,
                    OrderStatuses.DriverUnavailable
                },

            [OrderStatuses.DriverAssigned] =
                new[]
                {
                    OrderStatuses.DriverAccepted,
                    OrderStatuses.DriverDeclined
                },

            [OrderStatuses.DriverAccepted] =
                new[]
                {
                    OrderStatuses.WaitingForPickup
                },

            [OrderStatuses.WaitingForPickup] =
                new[]
                {
                    OrderStatuses.PickedUp
                },

            [OrderStatuses.PickedUp] =
                new[]
                {
                    OrderStatuses.EnRoute
                },

            [OrderStatuses.EnRoute] =
                new[]
                {
                    OrderStatuses.Arrived,
                    OrderStatuses.Delivered,
                    OrderStatuses.DeliveryFailed
                },

            [OrderStatuses.Arrived] =
                new[]
                {
                    OrderStatuses.Delivered,
                    OrderStatuses.DeliveryFailed
                },

            [OrderStatuses.Delivered] =
                new[]
                {
                    OrderStatuses.ProofUploaded
                },

            [OrderStatuses.ProofUploaded] =
                new[]
                {
                    OrderStatuses.SettlementPending
                },

            [OrderStatuses.SettlementPending] =
                new[]
                {
                    OrderStatuses.SettlementCalculating,
                    OrderStatuses.SettlementException
                },

            [OrderStatuses.SettlementCalculating] =
                new[]
                {
                    OrderStatuses.ReadyForPayout,
                    OrderStatuses.SettlementException
                },

            [OrderStatuses.ReadyForPayout] =
                new[]
                {
                    OrderStatuses.PayoutPending
                },

            [OrderStatuses.PayoutPending] =
                new[]
                {
                    OrderStatuses.Completed
                }
        };

    public bool CanTransition(
        string currentStatus,
        string nextStatus)
    {
        if (!AllowedTransitions.TryGetValue(
                currentStatus,
                out var allowed))
        {
            return false;
        }

        return allowed.Contains(
            nextStatus,
            StringComparer.OrdinalIgnoreCase);
    }

    public void ValidateTransition(
        string currentStatus,
        string nextStatus)
    {
        if (!CanTransition(currentStatus, nextStatus))
        {
            throw new InvalidOperationException(
                $"Invalid order transition: " +
                $"{currentStatus} -> {nextStatus}");
        }
    }
}