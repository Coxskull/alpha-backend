using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Alpha.API.Services;

public static class AlertService
{
    public static async Task<object> GenerateAlerts(
        AppDbContext context)
    {
        var alerts = new List<object>();

        var supplierOverdue =
            await context.Orders
                .Where(x =>
                    x.Status == "pending" &&
                    x.SupplierId == null)
                .ToListAsync();

        foreach (var order in supplierOverdue)
        {
            alerts.Add(new
            {
                id = Guid.NewGuid(),
                alertType = "supplier_overdue",
                message =
                    $"Order {order.OrderNumber} has no supplier assigned",
                createdAt = DateTime.UtcNow
            });
        }

        var driverOverdue =
            await context.Orders
                .Where(x =>
                    x.Status == "supplier_assigned" &&
                    x.DriverId == null)
                .ToListAsync();

        foreach (var order in driverOverdue)
        {
            alerts.Add(new
            {
                id = Guid.NewGuid(),
                alertType = "driver_overdue",
                message =
                    $"Order {order.OrderNumber} has no driver assigned",
                createdAt = DateTime.UtcNow
            });
        }

        return alerts;
    }
}