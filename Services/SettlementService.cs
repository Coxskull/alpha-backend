using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Alpha.API.Services;

public class SettlementService
{
    private readonly AppDbContext _context;
    private readonly TaxEngineService _taxEngine;

    public SettlementService(AppDbContext context, TaxEngineService taxEngine)
    {
        _context = context;
        _taxEngine = taxEngine;
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

        if (!string.IsNullOrWhiteSpace(payment.Currency))
            financial.Currency = payment.Currency;

        financial.ProcessingFee = 0;
        financial.FinancialStatus = "pending";
        financial.PayoutStatus = "not_ready";
        financial.SettlementStatus = "pending";

        await _context.SaveChangesAsync();

        return financial;
    }

    public async Task<OrderFinancial> VerifySettlement(
    Guid orderId,
    CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
            throw new Exception("Order not found.");

        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);

        var financial = await _context.OrderFinancials.FirstOrDefaultAsync(f => f.OrderId == orderId);

        var proofUploaded = await _context.DeliveryProofs.AnyAsync(p => p.OrderId == orderId);

        if (payment == null || financial == null)
            throw new Exception("Missing payment or financial record.");

        var paymentSuccessful = payment.PaymentStatus == "paid";
        var supplierComplete = order.SupplierId != null;

        var driverComplete =
            order.DriverId != null &&
            (order.Status == "delivered" || order.Status == "proof_uploaded");

        var hasProof =
            proofUploaded ||
            order.Status == "proof_uploaded" ||
            order.Status == "delivered";

        if (!paymentSuccessful || !supplierComplete || !driverComplete || !hasProof)
        {
            financial.FinancialStatus = "under_review";
            financial.PayoutStatus = "not_ready";
            financial.SettlementStatus = "pending";

            await _context.SaveChangesAsync();
            return financial;
        }

        await EnsureTaxCalculated(
     orderId,
     order.CountryCode,
     order.Currency,
     cancellationToken);

        financial.CustomerPaid = payment.Amount;
        financial.ProcessingFee = 0;

        ApplyTaxAwareSettlement(financial);

        if (!Reconciles(financial))
        {
            financial.FinancialStatus = "reconciliation_failed";
            financial.PayoutStatus = "blocked";
            financial.SettlementStatus = "blocked";

            await CreateFinancialException(orderId, financial.ReconciliationDifference);

            await _context.SaveChangesAsync();
            return financial;
        }

        financial.FinancialStatus = "verified";
        financial.PayoutStatus = "ready_for_payout";
        financial.SettlementStatus = "ready_for_payout";

        await CreateSettlementQueueItems(financial, order);

        await _context.SaveChangesAsync();

        return financial;
    }

    private async Task EnsureTaxCalculated(
    Guid orderId,
    string countryCode,
    string currency,
    CancellationToken cancellationToken = default)
    {
        var alreadyCalculated =
            await _context.TaxCalculations
                .AnyAsync(
                    x => x.OrderId == orderId,
                    cancellationToken);

        if (alreadyCalculated)
            return;

        await _taxEngine.CalculateOrderTaxes(
            orderId: orderId,
            country: countryCode,
            region: null,
            currency: currency,
            cancellationToken: cancellationToken
        );
    }

    private void ApplyTaxAwareSettlement(
     OrderFinancial financial)
    {
        financial.TaxCollected =
            financial.Tax;

        financial.SupplierNetPayable =
            Math.Max(
                0m,
                financial.SupplierAmount -
                financial.TaxWithheld
            );

        financial.DriverNetPayable =
            Math.Max(
                0m,
                financial.DriverAmount
            );

        financial.MechanicNetPayable =
            Math.Max(
                0m,
                financial.MechanicAmount
            );

        // Driver receives 70% of delivery fee.
        // Alpha retains the remaining 30%.
        var alphaDeliveryRevenue =
            Math.Max(
                0m,
                financial.DeliveryFee -
                financial.DriverNetPayable
            );

        financial.AlphaGrossDeliveryCommission =
            alphaDeliveryRevenue;

        financial.AlphaGrossPlatformCommission =
            financial.AlphaGrossPartsCommission +
            financial.AlphaGrossMechanicCommission +
            financial.AlphaGrossDeliveryCommission;

        financial.AlphaNetRevenue =
            Math.Max(
                0m,
                financial.AlphaGrossPlatformCommission +
                financial.ServiceFee -
                financial.ProcessingFee
            );

        financial.TotalAmount =
            financial.ItemSubtotal +
            financial.DeliveryFee +
            financial.ServiceFee +
            financial.MechanicAmount +
            financial.TaxCollected -
            financial.Discount;
    }

    private bool Reconciles(OrderFinancial financial)
    {
        var expected =
            financial.SupplierNetPayable +
            financial.DriverNetPayable +
            financial.MechanicNetPayable +
            financial.AlphaNetRevenue +
            financial.TaxCollected +
            financial.ProcessingFee +
            financial.RefundAmount +
            financial.DisputeReserve;

        financial.ReconciliationDifference = Math.Round(
            financial.CustomerPaid - expected,
            2
        );

        return Math.Abs(financial.ReconciliationDifference) <= 0.01m;
    }

    private async Task CreateFinancialException(Guid orderId, decimal difference)
    {
        var existing = await _context.OperationalAlerts
            .AnyAsync(x =>
                x.OrderId == orderId &&
                x.AlertType == "financial_exception" &&
                !x.Resolved
            );

        if (existing)
            return;

        _context.OperationalAlerts.Add(new OperationalAlert
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            AlertType = "financial_exception",
            Message = $"Settlement blocked. Reconciliation difference: {difference}. Review tax, provider payable, Alpha revenue, processing fee, or customer payment.",
            Resolved = false,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task CreateSettlementQueueItems(OrderFinancial financial, Order order)
    {
        var existingPayees =
     await _context.SettlementQueue
         .Where(x =>
             x.OrderFinancialId == financial.Id)
         .Select(x => x.PayeeType)
         .ToListAsync();

        if (
    financial.SupplierNetPayable > 0 &&
    !existingPayees.Contains("supplier"))
        {
            _context.SettlementQueue.Add(
                new SettlementQueue
                {
                    Id = Guid.NewGuid(),
                    OrderFinancialId = financial.Id,
                    PayeeType = "supplier",
                    PayeeId = order.SupplierId,
                    Amount = financial.SupplierNetPayable,
                    Status = "ready_for_payout",
                    CreatedAt = DateTime.UtcNow
                });
        }

        if (
    financial.DriverNetPayable > 0 &&
    !existingPayees.Contains("driver"))
        {
            _context.SettlementQueue.Add(
                new SettlementQueue
                {
                    Id = Guid.NewGuid(),
                    OrderFinancialId = financial.Id,
                    PayeeType = "driver",
                    PayeeId = order.DriverId,
                    Amount = financial.DriverNetPayable,
                    Status = "ready_for_payout",
                    CreatedAt = DateTime.UtcNow
                });
        }

        if (
    financial.MechanicNetPayable > 0 &&
    !existingPayees.Contains("mechanic"))
        {
            _context.SettlementQueue.Add(
                new SettlementQueue
                {
                    Id = Guid.NewGuid(),
                    OrderFinancialId = financial.Id,
                    PayeeType = "mechanic",
                    PayeeId = null,
                    Amount = financial.MechanicNetPayable,
                    Status = "ready_for_payout",
                    CreatedAt = DateTime.UtcNow
                });
        }
    }

    public async Task<OrderFinancial> VerifySettlementAfterProof(Guid orderId)
    {
        var financial = await VerifySettlement(orderId);

        if (financial.PayoutStatus == "blocked")
            return financial;

        if (financial.FinancialStatus == "verified")
        {
            financial.SettlementStatus = "ready_for_payout";
            financial.PayoutStatus = "ready_for_payout";

            await _context.SaveChangesAsync();
        }

        return financial;
    }
}