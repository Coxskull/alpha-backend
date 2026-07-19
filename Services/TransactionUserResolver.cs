using Alpha.API.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Services;

public class TransactionUserResolver
{
    private readonly AppDbContext _context;

    public TransactionUserResolver(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Guid>> ResolveOrderUserIdsAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == orderId,
                cancellationToken
            );

        if (order == null)
        {
            return new List<Guid>();
        }

        var userIds = new HashSet<Guid>();

        if (order.CustomerId.HasValue)
        {
            userIds.Add(order.CustomerId.Value);
        }

        if (order.SupplierId.HasValue)
        {
            var supplierUserId = await _context.Suppliers
                .Where(x => x.Id == order.SupplierId.Value)
                .Select(x => (Guid?)x.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (supplierUserId.HasValue)
            {
                userIds.Add(supplierUserId.Value);
            }
        }

        if (order.DriverId.HasValue)
        {
            var driverUserId = await _context.Drivers
                .Where(x => x.Id == order.DriverId.Value)
                .Select(x => (Guid?)x.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (driverUserId.HasValue)
            {
                userIds.Add(driverUserId.Value);
            }
        }

        return userIds.ToList();
    }
}