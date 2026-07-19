using System;
using System.Threading;
using System.Threading.Tasks;

public async Task RecordActivityAsync(
    Guid userId,
    string activityType,
    decimal grossAmount,
    CancellationToken cancellationToken = default)
{
    var activity = await _context.MemberBusinessActivities
        .FirstOrDefaultAsync(
            item => item.UserId == userId,
            cancellationToken
        );

    var now = DateTime.UtcNow;

    if (activity == null)
    {
        activity = new MemberBusinessActivity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FirstBusinessActivityAt = now,
            CreatedAt = now
        };

        _context.MemberBusinessActivities.Add(activity);
    }

    switch (activityType)
    {
        case "customer_purchase":
            activity.CustomerPurchases++;
            break;

        case "driver_delivery":
            activity.CompletedDeliveries++;
            break;

        case "supplier_fulfillment":
            activity.FulfilledPartsOrders++;
            break;

        case "mechanic_service":
            activity.CompletedServiceRequests++;
            break;
    }

    activity.GrossTransactionValue += grossAmount;
    activity.LastBusinessActivityAt = now;
    activity.IsBusinessActive = true;
    activity.UpdatedAt = now;

    await _context.SaveChangesAsync(cancellationToken);
}