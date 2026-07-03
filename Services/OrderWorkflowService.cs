public class OrderWorkflowService
{
    public string NextAfterSupplierAccepted()
    {
        return OrderStatuses.WaitingForDriver;
    }

    public string NextAfterDriverAccepted()
    {
        return OrderStatuses.WaitingForPickup;
    }

    public string NextAfterPickup()
    {
        return OrderStatuses.EnRoute;
    }
}