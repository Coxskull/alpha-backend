namespace Alpha.API.Models;

public enum ReferralBusinessEvent
{
    CustomerPurchase = 1,
    SupplierFulfilled = 2,
    DriverDelivered = 3,
    MechanicServiceCompleted = 4,
    ServiceRequestCompleted = 5,
    OrderCompleted = 6,
    PaymentCaptured = 7,
    RefundProcessed = 8,
    TransactionReversed = 9
}