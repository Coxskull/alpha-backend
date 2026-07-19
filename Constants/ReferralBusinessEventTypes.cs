namespace Alpha.API.Constants;

public static class ReferralBusinessEventTypes
{
    public const string CustomerPurchase =
        "customer_purchase";

    public const string SupplierFulfilled =
        "supplier_fulfilled";

    public const string DriverDelivered =
        "driver_delivered";

    public const string MechanicServiceCompleted =
        "mechanic_service_completed";

    public const string ServiceRequestCompleted =
        "service_request_completed";

    public const string OrderCompleted =
        "order_completed";

    public const string PaymentCaptured =
        "payment_captured";

    public const string RefundProcessed =
        "refund_processed";

    public const string TransactionReversed =
        "transaction_reversed";
}