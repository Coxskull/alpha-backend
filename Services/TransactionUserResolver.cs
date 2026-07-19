using Alpha.API.Data;
using Microsoft.EntityFrameworkCore;
using System;
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

    public async Task<Guid?> ResolveOrderActorAsync(
        Guid orderId,
        string actorType,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Where(item => item.Id == orderId)
            .Select(item => new
            {
                item.Id,
                item.SupplierId,
                item.DriverId,
                item.MechanicId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (order == null)
        {
            return null;
        }

        return actorType.ToLowerInvariant() switch
        {
            "supplier" or "provider" =>
                order.SupplierId == null
                    ? null
                    : await _context.Suppliers
                        .Where(item => item.Id == order.SupplierId)
                        .Select(item => item.UserId)
                        .FirstOrDefaultAsync(cancellationToken),

            "driver" =>
                order.DriverId == null
                    ? null
                    : await _context.Drivers
                        .Where(item => item.Id == order.DriverId)
                        .Select(item => item.UserId)
                        .FirstOrDefaultAsync(cancellationToken),

            "mechanic" =>
                order.MechanicId == null
                    ? null
                    : await _context.Mechanics
                        .Where(item => item.Id == order.MechanicId)
                        .Select(item => item.UserId)
                        .FirstOrDefaultAsync(cancellationToken),

            _ => null
        };
    }
}