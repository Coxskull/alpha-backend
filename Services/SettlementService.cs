using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using Alpha.API.Services.Entrepreneur;
namespace Alpha.API.Services;

public class SettlementService
{
    private readonly AppDbContext _context;
    private readonly TaxEngineService _taxEngine;
    private readonly EntrepreneurCommissionService
        _entrepreneurCommissionService;

    public SettlementService(
        AppDbContext context,
        TaxEngineService taxEngine,
        EntrepreneurCommissionService entrepreneurCommissionService)
    {
        _context = context;
        _taxEngine = taxEngine;
        _entrepreneurCommissionService =
            entrepreneurCommissionService;
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

        financial.ProcessingFee =
    payment.GatewayFee ?? 0m;
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
        var order = await _context.Orders
            .FirstOrDefaultAsync(
                x => x.Id == orderId,
                cancellationToken);

        if (order == null)
            throw new InvalidOperationException("Order not found.");

        var payment = await _context.Payments
            .FirstOrDefaultAsync(
                x => x.OrderId == orderId,
                cancellationToken);

        var financial = await _context.OrderFinancials
            .FirstOrDefaultAsync(
                x => x.OrderId == orderId,
                cancellationToken);

        if (payment == null)
            throw new InvalidOperationException(
                "Payment record not found.");

        if (financial == null)
            throw new InvalidOperationException(
                "Financial record not found.");

        var hasProof =
            await _context.DeliveryProofs
                .AnyAsync(
                    x => x.OrderId == orderId,
                    cancellationToken);

        var paymentSuccessful =
            string.Equals(
                payment.PaymentStatus,
                "paid",
                StringComparison.OrdinalIgnoreCase);

        var supplierComplete =
            await _context.OrderItems
                .AnyAsync(
                    x =>
                        x.OrderId == orderId &&
                        x.SupplierId.HasValue,
                    cancellationToken);

        var fulfillmentComplete =
            order.DriverId.HasValue &&
            order.Status == OrderStatuses.ProofUploaded &&
            hasProof;

        if (!paymentSuccessful ||
            !supplierComplete ||
            !fulfillmentComplete)
        {
            financial.FinancialStatus = "under_review";
            financial.PayoutStatus = "not_ready";
            financial.SettlementStatus = "pending";

            await _context.SaveChangesAsync(
                cancellationToken);

            return financial;
        }

        order.Status = OrderStatuses.SettlementPending;
        order.UpdatedAt = DateTime.UtcNow;

        financial.CustomerPaid = payment.Amount;

        financial.ProcessingFee =
            payment.GatewayFee ?? 0m;

        await CalculateOrRecalculateTax(
            order.Id,
            order.CountryCode,
            order.Currency,
            cancellationToken);

        ApplyTaxAwareSettlement(financial);

        financial.DirectTransactionCosts =
            await _entrepreneurDirectTransactionCostService
                .CalculateAsync(
                    order.Id,
                    cancellationToken);

        financial.AlphaEligibleNetPlatformRevenue =
            Math.Max(
                0m,
                financial.AlphaGrossPlatformCommission
                + financial.ServiceFee
                - financial.DirectTransactionCosts
                - financial.ProcessingFee);

        financial.EntrepreneurCommission = 0m;

        financial.AlphaRetainedRevenue =
            financial.AlphaEligibleNetPlatformRevenue;

        if (!Reconciles(financial))
        {
            financial.FinancialStatus =
                "reconciliation_failed";

            financial.PayoutStatus =
                "blocked";

            financial.SettlementStatus =
                "blocked";

            await CreateFinancialException(
                orderId,
                financial.ReconciliationDifference);

            await _context.SaveChangesAsync(
                cancellationToken);

            return financial;
        }

        financial.FinancialStatus = "verified";
        financial.PayoutStatus = "ready_for_payout";
        financial.SettlementStatus = "ready_for_payout";

        await CreateSettlementQueueItems(
            financial,
            order);

        await _context.SaveChangesAsync(
            cancellationToken);

        await _entrepreneurCommissionService
            .GenerateForOrderAsync(
                order.Id,
                cancellationToken);

        var entrepreneurCommission =
            await _context.EntrepreneurEarnings
                .Where(x => x.OrderId == order.Id)
                .SumAsync(
                    x => x.EntrepreneurEarningsAmount,
                    cancellationToken);

        financial.EntrepreneurCommission =
            entrepreneurCommission;

        financial.AlphaRetainedRevenue =
            Math.Max(
                0m,
                financial.AlphaEligibleNetPlatformRevenue
                - entrepreneurCommission);

        order.Status = OrderStatuses.ReadyForPayout;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        return financial;
    }

    private async Task CalculateOrRecalculateTax(
    Guid orderId,
    string countryCode,
    string currency,
    CancellationToken cancellationToken)
    {
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

        financial.AlphaEligibleNetPlatformRevenue =
    Math.Max(
        0m,
        financial.AlphaGrossPlatformCommission
        + financial.ServiceFee
        - financial.DirectTransactionCosts
        - financial.ProcessingFee);

        financial.EntrepreneurCommission = 0m;

        financial.AlphaRetainedRevenue =
    financial.AlphaEligibleNetPlatformRevenue;

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

    private async Task CreateSettlementQueueItems(
    OrderFinancial financial,
    Order order)
    {
        // Get all suppliers that own products/items
        // contained in this order.
        var supplierIds = await _context.OrderItems
            .Where(x => x.OrderId == order.Id)
            .Select(x => x.SupplierId)
            .Distinct()
            .ToListAsync();

        // Get existing settlement payees for this financial record.
        // We use PayeeId as well because multiple suppliers
        // can now exist in the same order.
        var existingPayees = await _context.SettlementQueue
            .Where(x => x.OrderFinancialId == financial.Id)
            .Select(x => new
            {
                x.PayeeType,
                x.PayeeId
            })
            .ToListAsync();


        // ============================================================
        // SUPPLIER SETTLEMENTS
        // ============================================================

        var supplierGroups = await _context.OrderItems
    .Where(x => x.OrderId == order.Id)
    .GroupBy(x => x.SupplierId)
    .Select(g => new
    {
        SupplierId = g.Key,
        GrossAmount = g.Sum(x =>
            (x.UnitPrice ?? 0m) * x.Quantity)
    })
    .Where(x => x.GrossAmount > 0)
    .ToListAsync();

        var totalSupplierGross =
            supplierGroups.Sum(x => x.GrossAmount);

        var supplierPayable =
            financial.SupplierNetPayable;

        decimal allocatedSupplierAmount = 0m;

        foreach (var supplierGroup in supplierGroups)
        {
            if (supplierGroup.SupplierId == Guid.Empty)
                continue;

            var alreadyExists = existingPayees.Any(x =>
                x.PayeeType == "supplier" &&
                x.PayeeId == supplierGroup.SupplierId);

            if (alreadyExists)
                continue;

            decimal supplierAmount;

            var isLastSupplier =
                supplierGroup.SupplierId ==
                supplierGroups.Last().SupplierId;

            if (isLastSupplier)
            {
                // Prevent rounding differences.
                supplierAmount =
                    Math.Round(
                        supplierPayable -
                        allocatedSupplierAmount,
                        2,
                        MidpointRounding.AwayFromZero);
            }
            else
            {
                supplierAmount =
                    Math.Round(
                        supplierPayable *
                        supplierGroup.GrossAmount /
                        totalSupplierGross,
                        2,
                        MidpointRounding.AwayFromZero);
            }

            if (supplierAmount <= 0)
                continue;

            allocatedSupplierAmount += supplierAmount;

            _context.SettlementQueue.Add(
                new SettlementQueue
                {
                    Id = Guid.NewGuid(),

                    OrderFinancialId =
                        financial.Id,

                    PayeeType =
                        "supplier",

                    PayeeId =
                        supplierGroup.SupplierId,

                    Amount =
                        supplierAmount,

                    Status =
                        "ready_for_payout",

                    CreatedAt =
                        DateTime.UtcNow
                });
        }


        // ============================================================
        // DRIVER SETTLEMENT
        // ============================================================

        if (
            financial.DriverNetPayable > 0 &&
            order.DriverId.HasValue)
        {
            var driverAlreadyExists =
                existingPayees.Any(x =>
                    x.PayeeType == "driver" &&
                    x.PayeeId == order.DriverId);

            if (!driverAlreadyExists)
            {
                _context.SettlementQueue.Add(
                    new SettlementQueue
                    {
                        Id = Guid.NewGuid(),

                        OrderFinancialId =
                            financial.Id,

                        PayeeType =
                            "driver",

                        PayeeId =
                            order.DriverId,

                        Amount =
                            financial.DriverNetPayable,

                        Status =
                            "ready_for_payout",

                        CreatedAt =
                            DateTime.UtcNow
                    });
            }
        }


        // ============================================================
        // MECHANIC SETTLEMENT
        // ============================================================

        if (financial.MechanicNetPayable > 0)
        {
            var mechanicAlreadyExists =
                existingPayees.Any(x =>
                    x.PayeeType == "mechanic");

            if (!mechanicAlreadyExists)
            {
                _context.SettlementQueue.Add(
                    new SettlementQueue
                    {
                        Id = Guid.NewGuid(),

                        OrderFinancialId =
                            financial.Id,

                        PayeeType =
                            "mechanic",

                        PayeeId =
                            null,

                        Amount =
                            financial.MechanicNetPayable,

                        Status =
                            "ready_for_payout",

                        CreatedAt =
                            DateTime.UtcNow
                    });
            }
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