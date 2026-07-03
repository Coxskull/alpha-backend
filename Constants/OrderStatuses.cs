namespace Alpha.API.Constants;

public static class OrderStatuses
{
    public const string PaymentPending = "payment_pending";
    public const string PaymentPaid = "payment_paid";
    public const string WaitingForSupplier = "waiting_for_supplier";
    public const string SupplierAssigned = "supplier_assigned";
    public const string SupplierAccepted = "supplier_accepted";
    public const string WaitingForDriver = "waiting_for_driver";
    public const string DriverAssigned = "driver_assigned";
    public const string DriverAccepted = "driver_accepted";
    public const string WaitingForPickup = "waiting_for_pickup";
    public const string PickedUp = "picked_up";
    public const string EnRoute = "en_route";
    public const string Arrived = "arrived_at_destination";
    public const string Delivered = "delivered";
    public const string ProofUploaded = "proof_uploaded";
    public const string SettlementPending = "settlement_pending";
    public const string ReadyForPayout = "ready_for_payout";

    public const string SupplierDeclined = "supplier_declined";
    public const string DriverDeclined = "driver_declined";
    public const string SupplierUnavailable = "supplier_unavailable";
    public const string DriverUnavailable = "driver_unavailable";
    public const string CustomerCancelled = "customer_cancelled";
    public const string PaymentFailed = "payment_failed";
    public const string DeliveryFailed = "delivery_failed";
}