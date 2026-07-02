using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Alpha.API.Services;

public class SettlementService
{
    private readonly AppDbContext _context;

    public SettlementService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<OrderFinancial> CreateOrUpdateSettlementAfterPayment(Guid orderId)
    {
        var order = await _context.Orders
            .Include(o => o.Supplier)
            .Include(o => o.Driver)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
            throw new Exception("Order not found.");

        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.OrderId == orderId);

        if (payment == null || payment.PaymentStatus != "paid")
            throw new Exception("Payment is not successful.");

        var financial = await _context.OrderFinancials
            .FirstOrDefaultAsync(f => f.OrderId == orderId);

        if (financial == null)
            throw new Exception("Financial record not found.");

        financial.CustomerPaid = payment.Amount;
        financial.FinancialStatus = "pending";
        financial.PayoutStatus = "not_ready";

        await _context.SaveChangesAsync();

        return financial;
    }

    public async Task<OrderFinancial> VerifySettlement(Guid orderId)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) throw new Exception("Order not found.");

        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
        var financial = await _context.OrderFinancials.FirstOrDefaultAsync(f => f.OrderId == orderId);
        var proof = await _context.DeliveryProofs.AnyAsync(p => p.OrderId == orderId);

        if (payment == null || financial == null)
            throw new Exception("Missing payment or financial record.");

        var paymentSuccessful = payment.PaymentStatus == "paid";
        var supplierComplete = order.SupplierId != null;
        var driverComplete = order.DriverId != null &&
            (order.Status == "delivered" || order.Status == "proof_uploaded");
        var proofUploaded = proof || order.Status == "proof_uploaded";

        if (!paymentSuccessful || !supplierComplete || !driverComplete || !proofUploaded)
        {
            financial.FinancialStatus = "under_review";
            financial.PayoutStatus = "not_ready";
            await _context.SaveChangesAsync();
            return financial;
        }

        financial.FinancialStatus = "verified";
        financial.PayoutStatus = "ready_for_payout";

        await CreateSettlementQueueItems(financial, order);

        await _context.SaveChangesAsync();
        return financial;
    }

    private async Task CreateSettlementQueueItems(OrderFinancial financial, Order order)
    {
        var exists = await _context.SettlementQueue
            .AnyAsync(x => x.OrderFinancialId == financial.Id);

        if (exists) return;

        if (financial.SupplierAmount > 0)
        {
            _context.SettlementQueue.Add(new SettlementQueue
            {
                Id = Guid.NewGuid(),
                OrderFinancialId = financial.Id,
                PayeeType = "supplier",
                PayeeId = order.SupplierId,
                Amount = financial.SupplierAmount,
                Status = "ready_for_payout",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (financial.DriverAmount > 0)
        {
            _context.SettlementQueue.Add(new SettlementQueue
            {
                Id = Guid.NewGuid(),
                OrderFinancialId = financial.Id,
                PayeeType = "driver",
                PayeeId = order.DriverId,
                Amount = financial.DriverAmount,
                Status = "ready_for_payout",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (financial.MechanicAmount > 0)
        {
            _context.SettlementQueue.Add(new SettlementQueue
            {
                Id = Guid.NewGuid(),
                OrderFinancialId = financial.Id,
                PayeeType = "mechanic",
                Amount = financial.MechanicAmount,
                Status = "ready_for_payout",
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}