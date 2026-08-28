using Alpha.API.Constants;
using Alpha.API.Data;
using Alpha.API.Models;
using Alpha.API.Services.Entrepreneur;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Services;

public class SettlementService
{
    private readonly AppDbContext _context;
    private readonly TaxEngineService _taxEngine;
    private readonly EntrepreneurCommissionService _entrepreneurCommissionService;

    public SettlementService(
        AppDbContext context,
        TaxEngineService taxEngine,
        EntrepreneurCommissionService entrepreneurCommissionService)
    {
        _context = context;
        _taxEngine = taxEngine;
        _entrepreneurCommissionService = entrepreneurCommissionService;
    }

    // ============================================================
    // PAYMENT INITIALIZATION
    // ============================================================

    // IMPORTANT:
    // This does NOT perform final settlement.
    //
    // Payment only establishes the financial payment inputs.
    //
    // Workflow:
    //
    // PAYMENT PAID
    //      ↓
    // WAITING FOR SUPPLIER
    //
    // Final settlement happens only after:
    //
    // PROOF UPLOADED
    //      ↓
    // SETTLEMENT PENDING
    //      ↓
    // ADMIN VERIFY
    //
    // ============================================================

    public async Task<OrderFinancial> CreateOrUpdateSettlementAfterPayment(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(
                o => o.Id == orderId,
                cancellationToken);

        if (order == null)
            throw new InvalidOperationException("Order not found.");

        var payment = await _context.Payments
            .Where(p =>
                p.OrderId == orderId &&
                p.PaymentStatus == "paid")
            .OrderByDescending(p => p.PaidAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (payment == null)
        {
            throw new InvalidOperationException(
                "Successful payment not found.");
        }

        var financial = await _context.OrderFinancials
            .FirstOrDefaultAsync(
                f => f.OrderId == orderId,
                cancellationToken);

        if (financial == null)
        {
            financial = new OrderFinancial
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                CreatedAt = DateTime.UtcNow
            };

            _context.OrderFinancials.Add(financial);
        }

        financial.CustomerPaid = payment.Amount;

        if (!string.IsNullOrWhiteSpace(payment.Currency))
        {
            financial.Currency =
                payment.Currency.Trim().ToUpperInvariant();
        }

        // GatewayFee is decimal.
        financial.ProcessingFee = payment.GatewayFee;

        // This is NOT final settlement.
        financial.FinancialStatus = "pending";
        financial.SettlementStatus = "pending";
        financial.PayoutStatus = "not_ready";

        await _context.SaveChangesAsync(cancellationToken);

        return financial;
    }

    // ============================================================
    // MAIN SETTLEMENT WORKFLOW
    // ============================================================

    // SINGLE entry point for Admin settlement verification.
    //
    // WORKFLOW:
    //
    // SETTLEMENT PENDING
    //       ↓
    // VERIFY ELIGIBILITY
    //       ↓
    // CALCULATE FINANCIALS
    //       ↓
    // CALCULATE TAX
    //       ↓
    // CALCULATE PAYABLES
    //       ↓
    // RECONCILE
    //       ↓
    // CREATE SETTLEMENT QUEUE
    //       ↓
    // GENERATE ENTREPRENEUR COMMISSION
    //       ↓
    // READY FOR PAYOUT
    //
    // ============================================================

    public async Task<OrderFinancial> VerifySettlement(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            // --------------------------------------------------------
            // LOAD ORDER
            // --------------------------------------------------------

            var order = await _context.Orders
                .FirstOrDefaultAsync(
                    x => x.Id == orderId,
                    cancellationToken);

            if (order == null)
            {
                throw new InvalidOperationException(
                    "Order not found.");
            }

            // --------------------------------------------------------
            // LOAD PAYMENT
            // --------------------------------------------------------

            var payment = await _context.Payments
                .Where(x => x.OrderId == orderId)
                .OrderByDescending(x => x.PaidAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (payment == null)
            {
                throw new InvalidOperationException(
                    "Payment record not found.");
            }

            // --------------------------------------------------------
            // LOAD FINANCIAL RECORD
            // --------------------------------------------------------

            var financial = await _context.OrderFinancials
                .FirstOrDefaultAsync(
                    x => x.OrderId == orderId,
                    cancellationToken);

            if (financial == null)
            {
                throw new InvalidOperationException(
                    "Financial record not found.");
            }

            // --------------------------------------------------------
            // 1. VERIFY PAYMENT
            // --------------------------------------------------------

            var paymentSuccessful =
                string.Equals(
                    payment.PaymentStatus,
                    "paid",
                    StringComparison.OrdinalIgnoreCase);

            if (!paymentSuccessful)
            {
                return await BlockSettlement(
                    financial,
                    "Settlement cannot proceed because payment is not paid.",
                    cancellationToken,
                    transaction);
            }

            // --------------------------------------------------------
            // 2. VERIFY DELIVERY PROOF
            // --------------------------------------------------------

            var hasProof =
                await _context.DeliveryProofs
                    .AnyAsync(
                        x => x.OrderId == orderId,
                        cancellationToken);

            if (!hasProof)
            {
                return await BlockSettlement(
                    financial,
                    "Settlement cannot proceed because delivery proof is missing.",
                    cancellationToken,
                    transaction);
            }

            // --------------------------------------------------------
            // 3. VERIFY SUPPLIER
            // --------------------------------------------------------

            // SupplierId is a non-nullable Guid in the current model.
            // Therefore HasValue cannot be used.
            var supplierComplete =
                await _context.OrderItems
                    .AnyAsync(
                        x =>
                            x.OrderId == orderId &&
                            x.SupplierId != Guid.Empty,
                        cancellationToken);

            if (!supplierComplete)
            {
                return await BlockSettlement(
                    financial,
                    "Settlement cannot proceed because supplier assignment is missing.",
                    cancellationToken,
                    transaction);
            }

            // --------------------------------------------------------
            // 4. VERIFY DRIVER
            // --------------------------------------------------------

            // DriverId is nullable in the current Order model.
            if (!order.DriverId.HasValue)
            {
                return await BlockSettlement(
                    financial,
                    "Settlement cannot proceed because driver assignment is missing.",
                    cancellationToken,
                    transaction);
            }

            // --------------------------------------------------------
            // 5. VERIFY ORDER STATUS
            // --------------------------------------------------------

            if (!string.Equals(
                    order.Status,
                    OrderStatuses.SettlementPending,
                    StringComparison.OrdinalIgnoreCase))
            {
                // Allow idempotent retry for already processed orders.
                if (string.Equals(
                        financial.FinancialStatus,
                        "verified",
                        StringComparison.OrdinalIgnoreCase))
                {
                    await transaction.CommitAsync(
                        cancellationToken);

                    return financial;
                }

                throw new InvalidOperationException(
                    $"Order is not pending settlement. Current status: {order.Status}");
            }

            // --------------------------------------------------------
            // 6. MARK CALCULATING
            // --------------------------------------------------------

            financial.FinancialStatus = "calculating";
            financial.SettlementStatus = "calculating";
            financial.PayoutStatus = "not_ready";

            await _context.SaveChangesAsync(
                cancellationToken);

            // --------------------------------------------------------
            // 7. LOAD PAYMENT VALUES
            // --------------------------------------------------------

            financial.CustomerPaid = payment.Amount;

            if (!string.IsNullOrWhiteSpace(payment.Currency))
            {
                financial.Currency =
                    payment.Currency.Trim().ToUpperInvariant();
            }

            financial.ProcessingFee = payment.GatewayFee;

            // --------------------------------------------------------
            // 8. CALCULATE TAX
            // --------------------------------------------------------

            await CalculateOrRecalculateTax(
                order.Id,
                order.CountryCode,
                order.Currency,
                cancellationToken);

            // --------------------------------------------------------
            // 9. CALCULATE TAX-AWARE SETTLEMENT
            // --------------------------------------------------------

            ApplyTaxAwareSettlement(financial);

            // --------------------------------------------------------
            // 10. CALCULATE ALPHA ELIGIBLE REVENUE
            // --------------------------------------------------------

            financial.DirectTransactionCosts = 0m;

            financial.AlphaEligibleNetPlatformRevenue =
                Math.Max(
                    0m,
                    financial.AlphaGrossPlatformCommission
                    + financial.ServiceFee
                    - financial.DirectTransactionCosts
                    - financial.ProcessingFee);

            // Entrepreneur commission is generated later.
            financial.EntrepreneurCommission = 0m;

            financial.AlphaRetainedRevenue =
                financial.AlphaEligibleNetPlatformRevenue;

            // --------------------------------------------------------
            // 11. RECONCILIATION
            // --------------------------------------------------------

            if (!Reconciles(financial))
            {
                financial.FinancialStatus =
                    "reconciliation_failed";

                financial.PayoutStatus =
                    "blocked";

                financial.SettlementStatus =
                    "blocked";

                order.Status =
                    OrderStatuses.SettlementException;

                order.UpdatedAt =
                    DateTime.UtcNow;

                await CreateFinancialException(
                    orderId,
                    financial.ReconciliationDifference,
                    cancellationToken);

                await _context.SaveChangesAsync(
                    cancellationToken);

                await transaction.CommitAsync(
                    cancellationToken);

                return financial;
            }

            // --------------------------------------------------------
            // 12. MARK VERIFIED
            // --------------------------------------------------------

            financial.FinancialStatus = "verified";
            financial.SettlementStatus = "verified";
            financial.PayoutStatus = "not_ready";

            await _context.SaveChangesAsync(
                cancellationToken);

            // --------------------------------------------------------
            // 13. CREATE SETTLEMENT QUEUE
            // --------------------------------------------------------

            await CreateSettlementQueueItems(
                financial,
                order,
                cancellationToken);

            await _context.SaveChangesAsync(
                cancellationToken);

            // --------------------------------------------------------
            // 14. GENERATE ENTREPRENEUR COMMISSION
            // --------------------------------------------------------

            await _entrepreneurCommissionService
                .GenerateForOrderAsync(
                    order.Id,
                    cancellationToken);

            // --------------------------------------------------------
            // 15. LOAD GENERATED ENTREPRENEUR COMMISSION
            // --------------------------------------------------------

            var entrepreneurCommission =
                await _context.EntrepreneurEarnings
                    .Where(x => x.OrderId == order.Id)
                    .SumAsync(
                        x => x.EntrepreneurEarningsAmount,
                        cancellationToken);

            financial.EntrepreneurCommission =
                entrepreneurCommission;

            // --------------------------------------------------------
            // 16. FINAL ALPHA RETAINED REVENUE
            // --------------------------------------------------------

            financial.AlphaRetainedRevenue =
                Math.Max(
                    0m,
                    financial.AlphaEligibleNetPlatformRevenue
                    - entrepreneurCommission);

            // --------------------------------------------------------
            // 17. FINAL PAYOUT READY
            // --------------------------------------------------------

            financial.FinancialStatus =
                "verified";

            financial.SettlementStatus =
                "ready_for_payout";

            financial.PayoutStatus =
                "ready_for_payout";

            order.Status =
                OrderStatuses.ReadyForPayout;

            order.UpdatedAt =
                DateTime.UtcNow;

            await _context.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return financial;
        }
        catch
        {
            await transaction.RollbackAsync(
                cancellationToken);

            throw;
        }
    }

    // ============================================================
    // TAX
    // ============================================================

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
            cancellationToken: cancellationToken);
    }

    // ============================================================
    // FINANCIAL CALCULATION
    // ============================================================

    private void ApplyTaxAwareSettlement(
        OrderFinancial financial)
    {
        financial.TaxCollected =
            financial.Tax;

        // --------------------------------------------------------
        // SUPPLIER
        // --------------------------------------------------------

        financial.SupplierNetPayable =
            Math.Max(
                0m,
                financial.SupplierAmount
                - financial.TaxWithheld);

        // --------------------------------------------------------
        // DRIVER
        // --------------------------------------------------------

        financial.DriverNetPayable =
            Math.Max(
                0m,
                financial.DriverAmount);

        // --------------------------------------------------------
        // MECHANIC
        // --------------------------------------------------------

        financial.MechanicNetPayable =
            Math.Max(
                0m,
                financial.MechanicAmount);

        // --------------------------------------------------------
        // DELIVERY REVENUE
        // --------------------------------------------------------

        var alphaDeliveryRevenue =
            Math.Max(
                0m,
                financial.DeliveryFee
                - financial.DriverNetPayable);

        financial.AlphaGrossDeliveryCommission =
            alphaDeliveryRevenue;

        // --------------------------------------------------------
        // ALPHA GROSS PLATFORM COMMISSION
        // --------------------------------------------------------

        financial.AlphaGrossPlatformCommission =
            financial.AlphaGrossPartsCommission
            + financial.AlphaGrossMechanicCommission
            + financial.AlphaGrossDeliveryCommission;

        // --------------------------------------------------------
        // ALPHA NET REVENUE BEFORE ENTREPRENEUR
        // --------------------------------------------------------

        financial.AlphaNetRevenue =
            Math.Max(
                0m,
                financial.AlphaGrossPlatformCommission
                + financial.ServiceFee
                - financial.ProcessingFee);

        // --------------------------------------------------------
        // ALPHA ELIGIBLE REVENUE
        // --------------------------------------------------------

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

        // --------------------------------------------------------
        // TOTAL ORDER AMOUNT
        // --------------------------------------------------------

        financial.TotalAmount =
            financial.ItemSubtotal
            + financial.DeliveryFee
            + financial.ServiceFee
            + financial.MechanicAmount
            + financial.TaxCollected
            - financial.Discount;
    }

    // ============================================================
    // RECONCILIATION
    // ============================================================

    private bool Reconciles(
        OrderFinancial financial)
    {
        var expected =
            financial.SupplierNetPayable
            + financial.DriverNetPayable
            + financial.MechanicNetPayable
            + financial.AlphaNetRevenue
            + financial.TaxCollected
            + financial.ProcessingFee
            + financial.RefundAmount
            + financial.DisputeReserve;

        financial.ReconciliationDifference =
            Math.Round(
                financial.CustomerPaid - expected,
                2,
                MidpointRounding.AwayFromZero);

        return Math.Abs(
            financial.ReconciliationDifference) <= 0.01m;
    }

    // ============================================================
    // FINANCIAL EXCEPTION
    // ============================================================

    private async Task CreateFinancialException(
        Guid orderId,
        decimal difference,
        CancellationToken cancellationToken)
    {
        var existing =
            await _context.OperationalAlerts
                .AnyAsync(
                    x =>
                        x.OrderId == orderId &&
                        x.AlertType == "financial_exception" &&
                        !x.Resolved,
                    cancellationToken);

        if (existing)
            return;

        _context.OperationalAlerts.Add(
            new OperationalAlert
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                AlertType = "financial_exception",
                Message =
                    $"Settlement blocked. " +
                    $"Reconciliation difference: {difference}. " +
                    $"Review customer payment, tax, supplier payable, " +
                    $"driver payable, Alpha revenue, processing fee, " +
                    $"refund, or dispute reserve.",
                Resolved = false,
                CreatedAt = DateTime.UtcNow
            });
    }

    // ============================================================
    // BLOCK SETTLEMENT
    // ============================================================

    private async Task<OrderFinancial> BlockSettlement(
        OrderFinancial financial,
        string reason,
        CancellationToken cancellationToken,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        financial.FinancialStatus = "under_review";
        financial.SettlementStatus = "blocked";
        financial.PayoutStatus = "blocked";

        await _context.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return financial;
    }

    // ============================================================
    // SETTLEMENT QUEUE
    // ============================================================

    private async Task CreateSettlementQueueItems(
        OrderFinancial financial,
        Order order,
        CancellationToken cancellationToken)
    {
        // --------------------------------------------------------
        // GET SUPPLIERS
        // --------------------------------------------------------

        var supplierGroups =
    await _context.OrderItems
        .Where(x =>
            x.OrderId == order.Id &&
            x.SupplierId != Guid.Empty)
        .GroupBy(x => x.SupplierId)
        .Select(g => new
        {
            SupplierId = g.Key,

            GrossAmount = g.Sum(
                x => (x.UnitPrice ?? 0m) * x.Quantity)
        })
        .Where(x =>
            x.GrossAmount > 0m)
        .OrderBy(x => x.SupplierId)
        .ToListAsync(
            cancellationToken);

        var eligibleSupplierGroups =
    supplierGroups
        .Where(x =>
            x.SupplierId != Guid.Empty &&
            x.GrossAmount > 0m)
        .ToList();

        // --------------------------------------------------------
        // EXISTING QUEUE ITEMS
        // --------------------------------------------------------

        var existingPayees =
            await _context.SettlementQueue
                .Where(
                    x =>
                        x.OrderFinancialId ==
                        financial.Id)
                .Select(
                    x => new
                    {
                        x.PayeeType,
                        x.PayeeId
                    })
                .ToListAsync(cancellationToken);

        // --------------------------------------------------------
        // SUPPLIER TOTAL
        // --------------------------------------------------------

        var totalSupplierGross =
            supplierGroups.Sum(
                x => x.GrossAmount ?? 0m);

        var supplierPayable =
            financial.SupplierNetPayable;

        decimal allocatedSupplierAmount = 0m;

        // --------------------------------------------------------
        // SUPPLIER QUEUE
        // --------------------------------------------------------

        for (
    var index = 0;
    index < eligibleSupplierGroups.Count;
    index++)
        {
            var supplierGroup =
                eligibleSupplierGroups[index];

            var supplierId =
                supplierGroup.SupplierId;

            var alreadyExists =
                existingPayees.Any(
                    x =>
                        x.PayeeType == "supplier" &&
                        x.PayeeId == supplierId);

            if (alreadyExists)
                continue;

            var isLastSupplier =
                index ==
                eligibleSupplierGroups.Count - 1;

            decimal supplierAmount;

            if (isLastSupplier)
            {
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

            if (supplierAmount <= 0m)
                continue;

            allocatedSupplierAmount +=
                supplierAmount;

            _context.SettlementQueue.Add(
                new SettlementQueue
                {
                    Id = Guid.NewGuid(),

                    OrderFinancialId =
                        financial.Id,

                    PayeeType =
                        "supplier",

                    PayeeId =
                        supplierId,

                    Amount =
                        supplierAmount,

                    Status =
                        "ready_for_payout",

                    CreatedAt =
                        DateTime.UtcNow
                });
        }

        // --------------------------------------------------------
        // DRIVER QUEUE
        // --------------------------------------------------------

        if (
            financial.DriverNetPayable > 0m &&
            order.DriverId.HasValue)
        {
            var driverId =
                order.DriverId.Value;

            var driverAlreadyExists =
                existingPayees.Any(
                    x =>
                        x.PayeeType == "driver" &&
                        x.PayeeId == driverId);

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
                            driverId,

                        Amount =
                            financial.DriverNetPayable,

                        Status =
                            "ready_for_payout",

                        CreatedAt =
                            DateTime.UtcNow
                    });
            }
        }

        // --------------------------------------------------------
        // MECHANIC QUEUE
        // --------------------------------------------------------

        if (
            financial.MechanicNetPayable > 0m)
        {
            var mechanicAlreadyExists =
                existingPayees.Any(
                    x =>
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

                        // Current model does not expose
                        // a mechanic ID here.
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

    // ============================================================
    // LEGACY COMPATIBILITY METHOD
    // ============================================================

    // Keep this temporarily if an existing controller calls it.
    // Eventually call VerifySettlement() directly.

    public async Task<OrderFinancial> VerifySettlementAfterProof(
        Guid orderId)
    {
        return await VerifySettlement(
            orderId,
            CancellationToken.None);
    }

    private async Task AddStatusHistory(
    Guid orderId,
    string status,
    CancellationToken cancellationToken)
    {
        _context.StatusHistory.Add(
            new StatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                Status = status,
                CreatedAt = DateTime.UtcNow
            });

        await _context.SaveChangesAsync(
            cancellationToken);
    }
}