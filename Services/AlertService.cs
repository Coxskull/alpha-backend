using System;
using System.Threading.Tasks;

namespace Alpha.API.Services;

public class AlertService
{
    private readonly AppDbContext _context;

    public AlertService(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task CheckOrderAlerts(
        Order order)
    {
        var age =
            DateTime.UtcNow -
            order.CreatedAt;

        if (
            order.Status == "pending"
            &&
            age.TotalMinutes > 10
        )
        {
            await CreateAlert(
                order.Id,
                "supplier_overdue",
                "No supplier assigned after 10 minutes"
            );
        }

        if (
            order.Status ==
            "supplier_assigned"
            &&
            age.TotalMinutes > 15
        )
        {
            await CreateAlert(
                order.Id,
                "driver_overdue",
                "No driver assigned after 15 minutes"
            );
        }

        if (
            order.Status != "delivered"
            &&
            age.TotalMinutes > 60
        )
        {
            await CreateAlert(
                order.Id,
                "delivery_delayed",
                "Delivery delayed"
            );
        }
    }

    private async Task CreateAlert(
        Guid orderId,
        string type,
        string message)
    {
        var exists =
            await _context
                .OperationalAlerts
                .AnyAsync(x =>
                    x.OrderId ==
                    orderId
                    &&
                    x.AlertType ==
                    type
                    &&
                    !x.Resolved);

        if (exists)
            return;

        _context.OperationalAlerts.Add(
            new OperationalAlert
            {
                OrderId = orderId,
                AlertType = type,
                Message = message,
                CreatedAt =
                    DateTime.UtcNow
            });

        await _context.SaveChangesAsync();
    }
}