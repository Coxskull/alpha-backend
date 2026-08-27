using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Alpha.API.Constants;
using Alpha.API.Services;
using Alpha.API.Services.Entrepreneur;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Alpha.API.Extensions;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly OrderWorkflowService _workflow;
    private readonly SettlementService _settlements;
    private readonly TaxEngineService _taxEngine;
    private readonly ReferralCommissionService _referralCommissionService;
    private readonly CountryCurrencyService _countryCurrencyService;
    private readonly AutoPartsCommissionService _autoPartsCommissionService;
    private readonly EntrepreneurCommissionService _entrepreneurCommissionService;

    public OrdersController(
        AppDbContext context,
        OrderWorkflowService workflow,
        SettlementService settlements,
        TaxEngineService taxEngine,
        ReferralCommissionService referralCommissionService,
        AutoPartsCommissionService autoPartsCommissionService,
        EntrepreneurCommissionService entrepreneurCommissionService,
        CountryCurrencyService countryCurrencyService)
    {
        _context = context;
        _workflow = workflow;
        _settlements = settlements;
        _taxEngine = taxEngine;
        _referralCommissionService = referralCommissionService;
        _countryCurrencyService = countryCurrencyService;
        _autoPartsCommissionService = autoPartsCommissionService;
        _entrepreneurCommissionService = entrepreneurCommissionService;
    }

    // =========================================================
    // CREATE ORDER
    // POST: /api/Orders
    // =========================================================

    [HttpPost]
    public async Task<IActionResult> CreateOrder(
    CreateOrderDto dto,
    CancellationToken cancellationToken)
    {
        await using var databaseTransaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // ---------------------------------------------------------
            // 1. Validate request
            // ---------------------------------------------------------

            if (dto == null)
            {
                return BadRequest(new
                {
                    message = "Order information is required."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.CustomerName))
            {
                return BadRequest(new
                {
                    message = "Customer name is required."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.DeliveryAddress))
            {
                return BadRequest(new
                {
                    message = "Delivery address is required."
                });
            }

            if (dto.Items == null || dto.Items.Count == 0)
            {
                return BadRequest(new
                {
                    message = "Cart is empty."
                });
            }

            if (dto.Items.Any(x => x.Quantity <= 0))
            {
                return BadRequest(new
                {
                    message = "Every item quantity must be greater than zero."
                });
            }

            // ---------------------------------------------------------
            // 2. Resolve country and currency
            // ---------------------------------------------------------

            var countryCode = string.IsNullOrWhiteSpace(dto.CountryCode)
                ? "MX"
                : dto.CountryCode.Trim().ToUpperInvariant();

            var allowedCountries = new[]
            {
            "PH",
            "MX",
            "US"
        };

            if (!allowedCountries.Contains(countryCode))
            {
                return BadRequest(new
                {
                    message = "Country must be PH, MX, or US."
                });
            }

            var requiredCurrency = countryCode switch
            {
                "PH" => "PHP",
                "MX" => "MXN",
                "US" => "USD",
                _ => throw new InvalidOperationException(
                    "Unable to determine currency.")
            };

            var currency = string.IsNullOrWhiteSpace(dto.Currency)
                ? requiredCurrency
                : dto.Currency.Trim().ToUpperInvariant();

            var allowedCurrencies = new[]
            {
            "PHP",
            "MXN",
            "USD"
        };

            if (!allowedCurrencies.Contains(currency))
            {
                return BadRequest(new
                {
                    message = "Currency must be PHP, MXN, or USD."
                });
            }

            if (currency != requiredCurrency)
            {
                return BadRequest(new
                {
                    message =
                        $"Orders from {countryCode} must use {requiredCurrency}."
                });
            }

            // ---------------------------------------------------------
            // 3. Validate payment method
            // ---------------------------------------------------------

            var paymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod)
                ? "cash"
                : dto.PaymentMethod.Trim().ToLowerInvariant();

            var allowedPaymentMethods = new[]
{
    "cash",
    "paypal",
    "paymongo_gcash",
    "stripe"
};

            if (!allowedPaymentMethods.Contains(paymentMethod))
            {
                return BadRequest(new
                {
                    message =
                        "Payment method must be cash, paypal, or paymongo_gcash."
                });
            }

            if (paymentMethod == "paymongo_gcash" &&
                countryCode != "PH")
            {
                return BadRequest(new
                {
                    message =
                        "GCash through PayMongo is available only for Philippine orders."
                });
            }

            if (paymentMethod == "paymongo_gcash" &&
                currency != "PHP")
            {
                return BadRequest(new
                {
                    message =
                        "GCash through PayMongo requires Philippine Peso or PHP."
                });
            }

            if (paymentMethod == "paypal" &&
                countryCode == "PH")
            {
                return BadRequest(new
                {
                    message =
                        "Use PayMongo GCash or cash for Philippine orders."
                });
            }

            if (paymentMethod == "stripe")
            {
                if (currency is not "PHP" and
                    not "MXN" and
                    not "USD")
                {
                    return BadRequest(new
                    {
                        message =
                            "Stripe does not support this currency."
                    });
                }
            }

            // ---------------------------------------------------------
            // 4. Combine duplicate cart items
            // ---------------------------------------------------------

            var normalizedItems = dto.Items
                .GroupBy(x => x.ProductId)
                .Select(group => new
                {
                    ProductId = group.Key,
                    Quantity = group.Sum(x => x.Quantity)
                })
                .ToList();

            var productIds = normalizedItems
                .Select(x => x.ProductId)
                .Distinct()
                .ToList();

            // ---------------------------------------------------------
            // 5. Load products
            // ---------------------------------------------------------

            var products = await _context.Products
                .Where(x =>
                    productIds.Contains(x.Id) &&
                    x.IsActive)
                .ToListAsync(cancellationToken);

            if (products.Count != productIds.Count)
            {
                var foundProductIds = products
                    .Select(x => x.Id)
                    .ToHashSet();

                var missingProductIds = productIds
                    .Where(x => !foundProductIds.Contains(x))
                    .ToList();

                return BadRequest(new
                {
                    message =
                        "One or more products are invalid or inactive.",
                    missingProductIds
                });
            }

            // ---------------------------------------------------------
            // 6. Validate product currency, country, and stock
            // ---------------------------------------------------------

            foreach (var item in normalizedItems)
            {
                var product = products.First(
                    x => x.Id == item.ProductId);

                if (item.Quantity <= 0)
                {
                    return BadRequest(new
                    {
                        message =
                            $"Quantity for {product.Name} must be greater than zero."
                    });
                }

                /*
                 * These validations require Product.Currency and
                 * Product.CountryCode properties.
                 *
                 * Remove these two blocks temporarily if those columns
                 * have not yet been added to your Product model.
                 */

                if (!string.IsNullOrWhiteSpace(product.Currency) &&
                    !string.Equals(
                        product.Currency,
                        currency,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        message =
                            $"{product.Name} is priced in {product.Currency}, " +
                            $"but the order uses {currency}."
                    });
                }

                if (!string.IsNullOrWhiteSpace(product.CountryCode) &&
                    !string.Equals(
                        product.CountryCode,
                        countryCode,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        message =
                            $"{product.Name} is not available for country {countryCode}."
                    });
                }

                if (product.QuantityAvailable < item.Quantity)
                {
                    return BadRequest(new
                    {
                        message =
                            $"{product.Name} does not have enough stock.",
                        requestedQuantity = item.Quantity,
                        availableQuantity = product.QuantityAvailable
                    });
                }
            }

            // ---------------------------------------------------------
            // 7. Calculate product subtotal
            // ---------------------------------------------------------

            decimal itemSubtotal = normalizedItems.Sum(item =>
            {
                var product = products.First(
                    x => x.Id == item.ProductId);

                return product.Price * item.Quantity;
            });

            itemSubtotal = Math.Round(
                itemSubtotal,
                2,
                MidpointRounding.AwayFromZero);

            AutoPartsCommissionResultDtos partsCommission;

            try
            {
                partsCommission =
                    await _autoPartsCommissionService.CalculateAsync(
                        itemSubtotal,
                        currency,
                        DateTime.UtcNow,
                        cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                await databaseTransaction.RollbackAsync(
                    CancellationToken.None);

                return UnprocessableEntity(new
                {
                    message = ex.Message,
                    code = "AUTO_PARTS_COMMISSION_POLICY_ERROR",
                    currency = currency
                });
            }


            // ---------------------------------------------------------
            // 8. Apply country-specific fees
            // ---------------------------------------------------------

            decimal deliveryFee = countryCode switch
            {
                "PH" => 100.00m,
                "MX" => 8.00m,
                "US" => 8.00m,
                _ => 0.00m
            };

            decimal serviceFee = countryCode switch
            {
                "PH" => 50.00m,
                "MX" => 3.00m,
                "US" => 3.00m,
                _ => 0.00m
            };

            decimal discount = 0.00m;
            decimal tax = 0.00m;
            decimal totalAmount = 0.00m;

            /*
             * The order values are stored directly in their selected
             * currency. Do not use a hard-coded exchange rate here.
             */
            decimal exchangeRate = 1.00m;

            // ---------------------------------------------------------
            // 9. Calculate preliminary earnings
            // ---------------------------------------------------------



            decimal supplierEarning = Math.Round(
    itemSubtotal - partsCommission.TotalCommission,
    2,
    MidpointRounding.AwayFromZero);

            if (supplierEarning < 0)
            {
                supplierEarning = 0m;
            }

            decimal driverEarning = Math.Round(
                deliveryFee * 0.70m,
                2,
                MidpointRounding.AwayFromZero);



            // ---------------------------------------------------------
            // 10. Get customer ID from authenticated user
            // ---------------------------------------------------------

            Guid? customerId = null;

            var userIdClaim =
                User.FindFirst(
                    System.Security.Claims.ClaimTypes.NameIdentifier)
                    ?.Value;

            if (string.IsNullOrWhiteSpace(userIdClaim))
            {
                userIdClaim =
                    User.FindFirst("sub")?.Value;
            }

            if (string.IsNullOrWhiteSpace(userIdClaim))
            {
                userIdClaim =
                    User.FindFirst("userId")?.Value;
            }

            if (Guid.TryParse(userIdClaim, out var authenticatedUserId))
            {
                var customerExists = await _context.Customers
                    .AnyAsync(
                        x => x.Id == authenticatedUserId,
                        cancellationToken);

                if (customerExists)
                {
                    customerId = authenticatedUserId;
                }
            }

            // ---------------------------------------------------------
            // 11. Create order
            // ---------------------------------------------------------

            var now = DateTime.UtcNow;

            var order = new Order
            {
                Id = Guid.NewGuid(),

                CustomerId = customerId,

                CustomerName = dto.CustomerName.Trim(),

                PickupAddress =
                    string.IsNullOrWhiteSpace(dto.PickupAddress)
                        ? dto.DeliveryAddress.Trim()
                        : dto.PickupAddress.Trim(),

                DeliveryAddress = dto.DeliveryAddress.Trim(),

                ItemDescription =
                    string.IsNullOrWhiteSpace(dto.ItemDescription)
                        ? string.Join(
                            ", ",
                            normalizedItems.Select(item =>
                            {
                                var product = products.First(
                                    x => x.Id == item.ProductId);

                                return $"{product.Name} x{item.Quantity}";
                            }))
                        : dto.ItemDescription.Trim(),

                Zone = string.IsNullOrWhiteSpace(dto.Zone)
                    ? countryCode
                    : dto.Zone.Trim(),

                CountryCode = countryCode,
                Currency = currency,

                OrderNumber =
                    $"ALPHA-{now:yyyyMMddHHmmssfff}",

                Status = OrderStatuses.PaymentPending,

                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Orders.Add(order);

            /*
             * Save the order first because order_items and
             * order_financials reference orders.id.
             */
            await _context.SaveChangesAsync(cancellationToken);

            // ---------------------------------------------------------
            // 12. Create order items and deduct stock
            // ---------------------------------------------------------

            foreach (var item in normalizedItems)
            {
                var product = products.First(
                    x => x.Id == item.ProductId);

                var orderItem = new OrderItem
                {
                    Id = Guid.NewGuid(),

                    OrderId = order.Id,

                    ProductId = product.Id,

                    SupplierId = product.SupplierId,

                    Quantity = item.Quantity,

                    UnitPrice = product.Price
                };

                _context.OrderItems.Add(orderItem);

                product.QuantityAvailable -= item.Quantity;
            }

            // ---------------------------------------------------------
            // 13. Create preliminary financial record
            // ---------------------------------------------------------

            var financialStatus =
                paymentMethod is "paypal" or "paymongo_gcash"
                    ? "awaiting_payment"
                    : "pending_review";

            var financial = new OrderFinancial
            {
                Id = Guid.NewGuid(),

                OrderId = order.Id,

                Currency = currency,

                ExchangeRate = exchangeRate,

                ItemSubtotal = itemSubtotal,

                DeliveryFee = deliveryFee,

                ServiceFee = serviceFee,

                Tax = tax,

                Discount = discount,

                TotalAmount = totalAmount,

                CustomerPaid = 0,

                // PARTS ECONOMICS
                SupplierAmount = supplierEarning,

                SupplierEarning = supplierEarning,

                // DELIVERY ECONOMICS
                DriverAmount = driverEarning,

                DriverEarning = driverEarning,

                // MECHANIC
                MechanicAmount = 0,

                // ALPHA
                AlphaPlatformFee =
        partsCommission.TotalCommission,

                CompanyRevenue =
        partsCommission.TotalCommission,

                SupplierNetPayable =
        supplierEarning,

                DriverNetPayable =
        driverEarning,

                MechanicNetPayable =
        0,

                AlphaNetRevenue =
        partsCommission.TotalCommission,

                FinancialStatus = financialStatus,

                PayoutStatus = "not_ready",

                SettlementStatus = "pending",

                CreatedAt = now
            };

            _context.OrderFinancials.Add(financial);

            await _context.SaveChangesAsync(cancellationToken);

            // ---------------------------------------------------------
            // 14. Calculate taxes
            // ---------------------------------------------------------

            /*
             * The tax engine should contain:
             *
             * PH = 12% VAT
             * MX = applicable IVA rate
             * US = rules based on the configured region
             *
             * Tax is calculated using the same order currency.
             */
            var taxBreakdown =
                await _taxEngine.CalculateOrderTaxes(
                    order.Id,
                    countryCode,
                    dto.Zone,
                    currency);

            // Reload because TaxEngineService updates this record.
            financial = await _context.OrderFinancials
                .FirstAsync(
                    x => x.OrderId == order.Id,
                    cancellationToken);

            totalAmount = financial.TotalAmount;
            tax = financial.Tax;

            if (financial.TotalAmount <= 0)
            {
                throw new InvalidOperationException(
                    "The calculated order total must be greater than zero.");
            }

            financial.AutoPartsCommission =
    partsCommission.TotalCommission;

            financial.AutoPartsCommissionRate =
                partsCommission.EffectiveCommissionRate;

            financial.AutoPartsCommissionPolicyId =
                partsCommission.PolicyId;

            financial.AutoPartsCommissionPolicyVersion =
                partsCommission.PolicyVersion;

            financial.PartsSupplierGross =
                itemSubtotal;

            financial.PartsSupplierNet =
                supplierEarning;

            var calculation =
    new AutoPartsCommissionCalculation
    {
        Id = Guid.NewGuid(),

        OrderId = order.Id,

        OrderFinancialId = financial.Id,

        PolicyId = partsCommission.PolicyId,

        PolicyVersion =
            partsCommission.PolicyVersion,

        Currency =
            partsCommission.Currency,

        PartsSubtotal =
            partsCommission.PartsSubtotal,

        TotalCommission =
            partsCommission.TotalCommission,

        EffectiveCommissionRate =
            partsCommission.EffectiveCommissionRate,

        CalculatedAt =
            DateTime.UtcNow,

        CreatedBy = "system"
    };

            financial.AlphaGrossPartsCommission =
    partsCommission.TotalCommission;

            financial.AlphaGrossMechanicCommission =
                0m;

            financial.AlphaGrossDeliveryCommission =
                0m;

            financial.AlphaGrossPlatformCommission =
                financial.AlphaGrossPartsCommission
                +
                financial.AlphaGrossMechanicCommission
                +
                financial.AlphaGrossDeliveryCommission;

            financial.DirectTransactionCosts =
                0m;

            financial.AlphaEligibleNetPlatformRevenue =
                financial.AlphaGrossPlatformCommission;

            financial.EntrepreneurCommission =
                0m;

            financial.AlphaRetainedRevenue =
                financial.AlphaEligibleNetPlatformRevenue;
           
            _context.AutoPartsCommissionCalculations
                .Add(calculation);

            foreach (var line in partsCommission.Lines)
            {
                _context.AutoPartsCommissionCalculationLines.Add(
                    new AutoPartsCommissionCalculationLine
                    {
                        Id = Guid.NewGuid(),

                        CalculationId =
                            calculation.Id,

                        TierId =
                            line.TierId,

                        TierOrder =
                            line.TierOrder,

                        TierMinimum =
                            line.TierMinimum,

                        TierMaximum =
                            line.TierMaximum,

                        TierPercentage =
                            line.TierPercentage,

                        AmountInTier =
                            line.AmountInTier,

                        CommissionAmount =
                            line.CommissionAmount,

                        CreatedAt =
                            DateTime.UtcNow
                    });
            }

            // ---------------------------------------------------------
            // 15. Create payment record
            // ---------------------------------------------------------

            var paymentGateway = paymentMethod switch
            {
                "paypal" => "paypal",
                "paymongo_gcash" => "paymongo",
                "stripe" => "stripe",
                _ => null
            };

            var paymentStatus = paymentMethod switch
            {
                "paypal" => "pending",
                "paymongo_gcash" => "pending",
                "stripe" => "pending",
                "cash" => "cash_pending",
                _ => "pending"
            };

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,

                Amount = financial.TotalAmount,
                Currency = currency,

                PaymentMethod = paymentMethod,
                PaymentGateway = paymentGateway,
                PaymentStatus = paymentStatus,

                TransactionReference = null,
                GatewayCheckoutSessionId = null,
                GatewayPaymentId = null,
                CheckoutUrl = null,
                FailureReason = null,

                RefundedAmount = 0,
                RefundStatus = "none",
                GatewayFee = 0,

                CreatedAt = now
            };

            _context.Payments.Add(payment);

            await _context.SaveChangesAsync(cancellationToken);

            // ---------------------------------------------------------
            // 16. Add status history and audit log
            // ---------------------------------------------------------

            await AddStatusHistory(
                order.Id,
                OrderStatuses.PaymentPending);

            await AddAuditLog(
     order.Id,
     paymentMethod switch
     {
         "stripe" =>
             "Order Created - Awaiting Stripe Payment",

         "paymongo_gcash" =>
             "Order Created - Awaiting PayMongo GCash Payment",

         "paypal" =>
             "Order Created - Awaiting PayPal Payment",

         _ =>
             "Order Created - Cash Payment Pending"
     });

            // ---------------------------------------------------------
            // 17. Commit transaction
            // ---------------------------------------------------------

            await databaseTransaction.CommitAsync(
                cancellationToken);

            // ---------------------------------------------------------
            // 18. Return result
            // ---------------------------------------------------------

            return Ok(new
            {
                message = "Order created successfully.",

                order = new
                {
                    order.Id,
                    order.OrderNumber,
                    order.CustomerId,
                    order.CustomerName,
                    order.PickupAddress,
                    order.DeliveryAddress,
                    order.ItemDescription,
                    order.Zone,
                    order.CountryCode,
                    order.Currency,
                    order.Status,
                    order.CreatedAt,
                    order.UpdatedAt
                },

                financial = new
                {
                    financial.Id,
                    financial.OrderId,
                    financial.Currency,
                    financial.ExchangeRate,

                    financial.ItemSubtotal,
                    financial.DeliveryFee,
                    financial.ServiceFee,
                    tax = financial.Tax,
                    financial.Discount,
                    financial.TotalAmount,

                    financial.SupplierAmount,
                    financial.DriverAmount,
                    financial.MechanicAmount,
                    financial.AlphaPlatformFee,

                    financial.SupplierEarning,
                    financial.DriverEarning,
                    financial.CompanyRevenue,

                    financial.FinancialStatus,
                    financial.PayoutStatus,
                    financial.SettlementStatus
                },

                payment = new
                {
                    payment.Id,
                    payment.OrderId,
                    payment.Amount,
                    payment.Currency,
                    payment.PaymentMethod,
                    payment.PaymentGateway,
                    payment.PaymentStatus,

                    requiresRedirect =
    paymentMethod is
        "paypal" or
        "paymongo_gcash" or
        "stripe",

                    paymentProvider = paymentGateway
                },

                taxBreakdown,

                items = normalizedItems.Select(item =>
                {
                    var product = products.First(
                        x => x.Id == item.ProductId);

                    return new
                    {
                        productId = product.Id,
                        productName = product.Name,
                        quantity = item.Quantity,
                        unitPrice = product.Price,
                        currency,
                        lineTotal = Math.Round(
                            product.Price * item.Quantity,
                            2,
                            MidpointRounding.AwayFromZero)
                    };
                })
            });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await databaseTransaction.RollbackAsync(
                cancellationToken);

            return Conflict(new
            {
                message =
                    "The product inventory changed while the order was being created. Please refresh your cart and try again.",
                error = ex.Message
            });
        }
        catch (DbUpdateException ex)
        {
            await databaseTransaction.RollbackAsync(
                cancellationToken);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message =
                        "A database error occurred while creating the order.",
                    error =
                        ex.InnerException?.Message ??
                        ex.Message
                });
        }
        catch (OperationCanceledException)
        {
            await databaseTransaction.RollbackAsync(
                CancellationToken.None);

            return StatusCode(
                StatusCodes.Status408RequestTimeout,
                new
                {
                    message =
                        "The order request was cancelled or timed out."
                });
        }
        catch (Exception ex)
        {
            await databaseTransaction.RollbackAsync(
                CancellationToken.None);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message =
                        "An unexpected error occurred while creating the order.",
                    error = ex.Message
                });
        }
    }

    // =========================================================
    // GET ORDER DETAILS
    // GET: /api/Orders/{id}
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> GetOrders()
    {
        try
        {
            var orders = await _context.Orders
                .Include(o => o.Supplier)
                .Include(o => o.Driver)
                .GroupJoin(
                    _context.DeliveryProofs,
                    order => order.Id,
                    proof => proof.OrderId,
                    (order, proofs) => new
                    {
                        Order = order,
                        Proof = proofs
                            .OrderByDescending(p => p.UploadedAt)
                            .FirstOrDefault()
                    }
                )
                .OrderByDescending(x => x.Order.CreatedAt)
                .Select(x => new
                {
                    id = x.Order.Id,
                    orderNumber = x.Order.OrderNumber,
                    customerName = x.Order.CustomerName,
                    pickupAddress = x.Order.PickupAddress,
                    deliveryAddress = x.Order.DeliveryAddress,
                    itemDescription = x.Order.ItemDescription,
                    zone = x.Order.Zone,
                    status = x.Order.Status,
                    createdAt = x.Order.CreatedAt,
                    updatedAt = x.Order.UpdatedAt,

                    supplierId = x.Order.SupplierId,
                    supplierName = x.Order.Supplier != null
                        ? x.Order.Supplier.Name
                        : null,

                    driverId = x.Order.DriverId,
                    driverName = x.Order.Driver != null
                        ? x.Order.Driver.FullName
                        : null,

                    proofImageUrl = x.Proof != null
                        ? x.Proof.ImageUrl
                        : null,

                    proofUploadedAt = x.Proof != null
                        ? x.Proof.UploadedAt
                        : (DateTime?)null
                })
                .ToListAsync();

            return Ok(orders);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.ToString());
        }
    }

    // =========================================================
    // GET STATUS HISTORY
    // GET: /api/Orders/{id}/status
    // =========================================================

    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetStatus(Guid id)
    {
        var history = await _context.StatusHistory
            .Where(x => x.OrderId == id)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        return Ok(history);
    }

    // =========================================================
    // ASSIGN SUPPLIER
    // POST: /api/Orders/{id}/assign-supplier
    // =========================================================
    [HttpPost("{id}/assign-supplier/{supplierId}")]
    public async Task<IActionResult> AssignSupplier(
    Guid id,
    Guid supplierId)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound("Order not found.");

        if (order.Status != OrderStatuses.WaitingForSupplier &&
    order.Status != OrderStatuses.PaymentPaid)
        {
            return BadRequest($"Supplier cannot be assigned while order status is {order.Status}.");
        }

        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == supplierId);

        if (supplier == null)
            return NotFound("Supplier not found.");

        if (supplier.AvailabilityStatus != "available")
        {
            return BadRequest(
                "Selected supplier is not available."
            );
        }

        order.SupplierId = supplier.Id;

        order.Status = OrderStatuses.SupplierAssigned;
        order.UpdatedAt = DateTime.UtcNow;

        supplier.AvailabilityStatus = "busy";

        supplier.CurrentWorkload++;

        await _context.SaveChangesAsync();

        // Optional if you already have these methods
        await AddStatusHistory(order.Id, OrderStatuses.SupplierAssigned);

        await AddAuditLog(
            order.Id,
            $"Supplier Assigned: {supplier.Name}"
        );

        return Ok(new
        {
            message = "Supplier assigned successfully",
            supplierId = supplier.Id,
            supplierName = supplier.Name,
            status = order.Status
        });
    }

    [HttpPost("{id}/assign-supplier")]
    public async Task<IActionResult> AssignSupplierAuto(Guid id)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound("Order not found.");

        if (order.Status != OrderStatuses.WaitingForSupplier &&
    order.Status != OrderStatuses.PaymentPaid)
        {
            return BadRequest($"Supplier cannot be assigned while order status is {order.Status}.");
        }

        var supplier = await _context.Suppliers
            .Where(s =>
                s.AvailabilityStatus.ToLower() == "available" &&
                s.Territory == order.Zone)
            .OrderBy(s => s.CurrentWorkload)
            .ThenByDescending(s => s.ResponseRate)
            .FirstOrDefaultAsync();

        supplier ??= await _context.Suppliers
            .Where(s => s.AvailabilityStatus.ToLower() == "available")
            .OrderBy(s => s.CurrentWorkload)
            .ThenByDescending(s => s.ResponseRate)
            .FirstOrDefaultAsync();

        if (supplier == null)
            return NotFound("No available supplier found.");

        order.SupplierId = supplier.Id;
        order.Status = OrderStatuses.SupplierAssigned;
        order.UpdatedAt = DateTime.UtcNow;

        supplier.AvailabilityStatus = "busy";
        supplier.CurrentWorkload += 1;

        await _context.SaveChangesAsync();

        await AddStatusHistory(order.Id, OrderStatuses.SupplierAssigned);
        await AddAuditLog(order.Id, $"Auto Supplier Assigned: {supplier.Name}");

        return Ok(new
        {
            message = "Supplier assigned successfully",
            supplierId = supplier.Id,
            supplierName = supplier.Name,
            status = order.Status
        });
    }

    // =========================================================
    // ASSIGN DRIVER
    // POST: /api/Orders/{id}/assign-driver
    // =========================================================

    [HttpPost("{id}/assign-driver/{driverId}")]
    public async Task<IActionResult> AssignDriver(
    Guid id,
    Guid driverId)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(x => x.Id == id);

        if (order == null)
            return NotFound();

        if (order.Status != OrderStatuses.WaitingForDriver)
        {
            return BadRequest(
                $"Driver cannot be assigned while order status is {order.Status}.");
        }

        var driver = await _context.Drivers
            .FirstOrDefaultAsync(x => x.Id == driverId);

        if (driver == null)
            return NotFound();

        if (driver.AvailabilityStatus != "available")
        {
            return BadRequest(
                "Driver is not available."
            );
        }

        order.DriverId = driver.Id;

        order.Status = OrderStatuses.DriverAssigned;

        order.UpdatedAt = DateTime.UtcNow;

        driver.AvailabilityStatus = "busy";

        driver.ActiveJobs++;

        await _context.SaveChangesAsync();

        await AddStatusHistory(order.Id, OrderStatuses.DriverAssigned);

        await AddAuditLog(
            order.Id,
            $"Driver Assigned: {driver.FullName}"
        );

        return Ok(new
        {
            message = "Driver assigned successfully",
            driverId = driver.Id,
            driverName = driver.FullName,
            status = order.Status
        });
    }

    [HttpPost("{id}/assign-driver")]
    public async Task<IActionResult> AssignDriverAuto(Guid id)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound("Order not found.");

        if (order.SupplierId == null)
            return BadRequest("Assign supplier first.");

        if (order.Status != OrderStatuses.WaitingForDriver &&
     order.Status != OrderStatuses.SupplierAccepted)
        {
            return BadRequest(
                $"Driver cannot be assigned while order status is {order.Status}."
            );
        }

        var driver = await _context.Drivers
            .Where(d =>
                d.AvailabilityStatus.ToLower() == "available" &&
                d.Territory == order.Zone)
            .OrderBy(d => d.ActiveJobs)
            .ThenByDescending(d => d.ResponseRate)
            .FirstOrDefaultAsync();

        driver ??= await _context.Drivers
            .Where(d => d.AvailabilityStatus.ToLower() == "available")
            .OrderBy(d => d.ActiveJobs)
            .ThenByDescending(d => d.ResponseRate)
            .FirstOrDefaultAsync();

        if (driver == null)
            return NotFound("No available driver found.");

        order.DriverId = driver.Id;
        order.Status = OrderStatuses.DriverAssigned;
        order.UpdatedAt = DateTime.UtcNow;

        driver.AvailabilityStatus = "busy";
        driver.ActiveJobs += 1;

        await _context.SaveChangesAsync();

        await AddStatusHistory(order.Id, OrderStatuses.DriverAssigned);
        await AddAuditLog(order.Id, $"Auto Driver Assigned: {driver.FullName}");

        return Ok(new
        {
            message = "Driver assigned successfully",
            driverId = driver.Id,
            driverName = driver.FullName,
            status = order.Status
        });
    }

    // =========================================================
    // PICKED UP
    // POST: /api/Orders/{id}/picked-up
    // =========================================================
    [HttpPost("{id:guid}/picked-up")]
    [Authorize(Roles = "driver")]
    public async Task<IActionResult> PickedUp(
    Guid id,
    CancellationToken cancellationToken)
    {
        var driver =
            await GetCurrentDriver(cancellationToken);

        if (driver == null)
            return Forbid();

        var order =
            await GetDriverOrder(
                id,
                driver.Id,
                cancellationToken);

        if (order == null)
        {
            return NotFound(
                new
                {
                    message =
                        "Order not found or is not assigned to you."
                });
        }

        // IMPORTANT:
        // Driver can only pick up after supplier
        // has marked the order ready for pickup.
        if (order.Status != OrderStatuses.WaitingForPickup)
        {
            return BadRequest(
                new
                {
                    message =
                        $"Order cannot be picked up while " +
                        $"status is '{order.Status}'. " +
                        $"Expected '{OrderStatuses.WaitingForPickup}'."
                });
        }

        order.Status =
            OrderStatuses.PickedUp;

        order.UpdatedAt =
            DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        await AddOrderStatusHistory(
            id,
            OrderStatuses.PickedUp,
            $"Picked up by driver {driver.Id}.",
            cancellationToken);

        await AddOrderAuditLog(
            id,
            $"Driver {driver.Id} picked up order.",
            cancellationToken);

        return Ok(
            new
            {
                success = true,
                orderId = order.Id,
                driverId = driver.Id,
                status = order.Status,
                updatedAt = order.UpdatedAt
            });
    }

    // =========================================================
    // EN ROUTE
    // POST: /api/Orders/{id}/en-route
    // =========================================================

    [HttpPost("{id:guid}/en-route")]
    [Authorize(Roles = "driver")]
    public async Task<IActionResult> EnRoute(
    Guid id,
    CancellationToken cancellationToken)
    {
        var driver =
            await GetCurrentDriver(cancellationToken);

        if (driver == null)
            return Forbid();

        var order =
            await GetDriverOrder(
                id,
                driver.Id,
                cancellationToken);

        if (order == null)
        {
            return NotFound(
                new
                {
                    message =
                        "Order not found or is not assigned to you."
                });
        }

        // IMPORTANT:
        // Driver must have picked up the order first.
        if (order.Status != OrderStatuses.PickedUp)
        {
            return BadRequest(
                new
                {
                    message =
                        $"Order cannot be marked en-route while " +
                        $"status is '{order.Status}'. " +
                        $"Expected '{OrderStatuses.PickedUp}'."
                });
        }

        order.Status =
            OrderStatuses.EnRoute;

        order.UpdatedAt =
            DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        await AddOrderStatusHistory(
            id,
            OrderStatuses.EnRoute,
            $"Driver {driver.Id} is en-route.",
            cancellationToken);

        await AddOrderAuditLog(
            id,
            $"Driver {driver.Id} marked order en-route.",
            cancellationToken);

        return Ok(
            new
            {
                success = true,
                orderId = order.Id,
                driverId = driver.Id,
                status = order.Status,
                updatedAt = order.UpdatedAt
            });
    }

    // =========================================================
    // DELIVERED
    // POST: /api/Orders/{id}/delivered
    // =========================================================
    [HttpPost("{id:guid}/delivered")]
    [Authorize(Roles = "driver")]
    public async Task<IActionResult> Delivered(
     Guid id,
     CancellationToken cancellationToken)
    {
        var driver =
            await GetCurrentDriver(cancellationToken);

        if (driver == null)
            return Forbid();

        var order =
            await GetDriverOrder(
                id,
                driver.Id,
                cancellationToken);

        if (order == null)
        {
            return NotFound(
                new
                {
                    message =
                        "Order not found or is not assigned to you."
                });
        }

        // IMPORTANT:
        // Driver must be en-route before delivery.
        if (order.Status != OrderStatuses.EnRoute)
        {
            return BadRequest(
                new
                {
                    message =
                        $"Order cannot be marked delivered while " +
                        $"status is '{order.Status}'. " +
                        $"Expected '{OrderStatuses.EnRoute}'."
                });
        }

        order.Status =
            OrderStatuses.Delivered;

        order.UpdatedAt =
            DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        await AddOrderStatusHistory(
            id,
            OrderStatuses.Delivered,
            $"Driver {driver.Id} marked order delivered.",
            cancellationToken);

        await AddOrderAuditLog(
            id,
            $"Driver {driver.Id} marked order delivered.",
            cancellationToken);

        return Ok(
            new
            {
                success = true,
                orderId = order.Id,
                driverId = driver.Id,
                status = order.Status,
                updatedAt = order.UpdatedAt
            });
    }

    // =========================================================
    // UPLOAD DELIVERY PROOF
    // POST: /api/Orders/{id}/proof
    // =========================================================

    [HttpPost("{id:guid}/proof")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> UploadProof(
    Guid id,
    [FromForm] UploadDeliveryProofDto dto,
    CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (order == null)
        {
            return NotFound(new
            {
                message = "Order not found."
            });
        }

        if (!string.Equals(
                order.Status,
                OrderStatuses.Delivered,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Order must be delivered first."
            });
        }

        if (dto.Image == null ||
            dto.Image.Length == 0)
        {
            return BadRequest(new
            {
                message = "Image is required."
            });
        }

        await using var memoryStream =
            new MemoryStream();

        await dto.Image.CopyToAsync(
            memoryStream,
            cancellationToken);

        var base64 =
            Convert.ToBase64String(
                memoryStream.ToArray());

        var imageUrl =
            $"data:{dto.Image.ContentType};base64,{base64}";

        var proof = new DeliveryProof
        {
            Id = Guid.NewGuid(),
            OrderId = id,
            ImageUrl = imageUrl,
            UploadedAt = DateTime.UtcNow
        };

        _context.DeliveryProofs.Add(proof);

        var financial = await _context.OrderFinancials
    .FirstOrDefaultAsync(
        x => x.OrderId == id,
        cancellationToken);

        if (financial != null)
        {
            financial.CompletionProofUrl = imageUrl;
            financial.FinancialStatus = "pending_review";
            financial.PayoutStatus = "not_ready";
            financial.SettlementStatus = "pending";
        }

        order.Status = OrderStatuses.ProofUploaded;

        await _context.SaveChangesAsync(cancellationToken);

        await AddStatusHistory(
            id,
            OrderStatuses.ProofUploaded);

        order.Status = OrderStatuses.SettlementPending;

        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await AddStatusHistory(
            id,
            OrderStatuses.SettlementPending);

        await AddAuditLog(
            id,
            "Delivery proof uploaded. Order is now pending settlement verification.");

        return Ok(new
        {
            success = true,
            message = "Delivery proof uploaded successfully.",
            orderId = id,
            status = order.Status,
            financialStatus = financial?.FinancialStatus,
            settlementStatus = financial?.SettlementStatus
        });
    }

    // =========================================================
    // HELPER: STATUS HISTORY
    // =========================================================

    private async Task AddStatusHistory(Guid orderId, string status)
    {
        var history = new StatusHistory
        {
            Id = Guid.NewGuid(),

            OrderId = orderId,

            Status = status,

            CreatedAt = DateTime.UtcNow
        };

        _context.StatusHistory.Add(history);

        await _context.SaveChangesAsync();
    }

    // =========================================================
    // HELPER: AUDIT LOG
    // =========================================================

    private async Task AddAuditLog(
    Guid orderId,
    string action,
    string performedBy = "System")
    {
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Action = action,
            PerformedBy = performedBy,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(log);

        await _context.SaveChangesAsync();
    }
    [HttpGet("{id}/details")]
    public async Task<IActionResult> GetDetails(Guid id)
    {
        var order = await _context.Orders
            .Include(x => x.Supplier)
            .Include(x => x.Driver)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (order == null)
            return NotFound();

        return Ok(new
        {
            id = order.Id,

            orderNumber = order.OrderNumber,

            customerName = order.CustomerName,

            itemDescription = order.ItemDescription,

            pickupAddress = order.PickupAddress,

            deliveryAddress = order.DeliveryAddress,

            zone = order.Zone,

            status = order.Status,

            supplierId = order.SupplierId,

            supplierName = order.Supplier != null
                ? order.Supplier.Name
                : null,

            driverId = order.DriverId,

            driverName = order.Driver != null
                ? order.Driver.FullName
                : null,

            createdAt = order.CreatedAt,

            updatedAt = order.UpdatedAt
        });
    }
    [HttpGet("{id}/recommendations")]
    public async Task<IActionResult> GetRecommendations(Guid id)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(x => x.Id == id);

        if (order == null)
            return NotFound();

        var supplier =
            await _context.Suppliers
                .Where(x =>
                    x.AvailabilityStatus == "available" &&
                    x.Territory == order.Zone)
                .OrderBy(x => x.CurrentWorkload)
                .FirstOrDefaultAsync();

        var driver =
            await _context.Drivers
                .Where(x =>
                    x.AvailabilityStatus == "available" &&
                    x.Territory == order.Zone)
                .OrderBy(x => x.ActiveJobs)
                .FirstOrDefaultAsync();

        return Ok(new
        {
            supplier,
            driver
        });
    }
    [HttpPost("{id}/confirm-payment")]
    public async Task<IActionResult> ConfirmPayment(Guid id, string transactionReference)
    {
        var order = await _context.Orders.FindAsync(id);

        if (order == null)
            return NotFound();

        var payment = await _context.Payments
            .FirstOrDefaultAsync(x => x.OrderId == id);

        if (payment == null)
            return NotFound("Payment record not found.");

        payment.PaymentStatus = "paid";
        payment.TransactionReference = transactionReference;
        payment.PaidAt = DateTime.UtcNow;

        order.Status = OrderStatuses.WaitingForSupplier;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await AddStatusHistory(id, OrderStatuses.PaymentPaid);
        await AddStatusHistory(id, OrderStatuses.WaitingForSupplier);
        await AddAuditLog(id, "Payment Confirmed");

        return Ok(new
        {
            message = "Payment confirmed. Order is now paid and pending dispatch.",
            orderStatus = order.Status,
            paymentStatus = payment.PaymentStatus
        });
    }
    [HttpPost("{id}/supplier-accept")]
    [Authorize(Roles = "supplier,provider")]
    public async Task<IActionResult> SupplierAccept(
    Guid id,
    CancellationToken cancellationToken)
    {
        Guid userId;

        try
        {
            userId = User.GetUserId();
        }
        catch
        {
            return Unauthorized();
        }

        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(
                x => x.UserId == userId,
                cancellationToken);

        if (supplier == null)
            return Forbid();

        var order = await _context.Orders
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (order == null)
            return NotFound();

        var ownsItem = await _context.OrderItems
            .AnyAsync(
                x =>
                    x.OrderId == id &&
                    x.SupplierId == supplier.Id,
                cancellationToken);

        if (!ownsItem)
        {
            return Forbid();
        }

        if (order.Status != OrderStatuses.SupplierAssigned &&
            order.Status != OrderStatuses.PaymentPaid &&
            order.Status != OrderStatuses.WaitingForSupplier)
        {
            return BadRequest(
                $"Supplier cannot accept order while status is {order.Status}.");
        }

        order.SupplierId = supplier.Id;
        order.Status = OrderStatuses.SupplierAccepted;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await AddStatusHistory(
            id,
            OrderStatuses.SupplierAccepted);

        await AddAuditLog(
            id,
            $"Supplier {supplier.Id} accepted order.");

        order.Status = OrderStatuses.WaitingForDriver;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await AddStatusHistory(
            id,
            OrderStatuses.WaitingForDriver);

        await AddAuditLog(
            id,
            "Order is now waiting for driver assignment.");

        return Ok(new
        {
            success = true,
            orderId = id,
            supplierId = supplier.Id,
            status = order.Status
        });
    }
    [HttpPost("{id}/ready-for-pickup")]
    [Authorize(Roles = "supplier,provider")]
    public async Task<IActionResult> ReadyForPickup(
    Guid id,
    CancellationToken cancellationToken)
    {
        Guid userId;

        try
        {
            userId = User.GetUserId();
        }
        catch
        {
            return Unauthorized();
        }

        var order = await _context.Orders
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (order == null)
            return NotFound();

        if (order.Status != OrderStatuses.DriverAccepted &&
            order.Status != OrderStatuses.WaitingForPickup)
        {
            return BadRequest(
                $"Order is not ready for pickup. Current status: {order.Status}");
        }

        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(
                x => x.UserId == userId,
                cancellationToken);

        if (supplier == null)
            return Forbid();

        var ownsItem = await _context.OrderItems
            .AnyAsync(
                x =>
                    x.OrderId == id &&
                    x.SupplierId == supplier.Id,
                cancellationToken);

        if (!ownsItem)
            return Forbid();

        order.Status = OrderStatuses.WaitingForPickup;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        await AddStatusHistory(
            id,
            OrderStatuses.WaitingForPickup);

        await AddAuditLog(
            id,
            $"Supplier {supplier.Id} marked order ready for pickup.");

        return Ok(new
        {
            success = true,
            orderId = id,
            supplierId = supplier.Id,
            status = order.Status
        });
    }

    [HttpPost("{id}/driver-accept")]
    [Authorize(Roles = "driver")]
    public async Task<IActionResult> DriverAccept(
    Guid id,
    CancellationToken cancellationToken)
    {
        var driver =
            await GetCurrentDriver(cancellationToken);

        if (driver == null)
            return Forbid();

        var order = await _context.Orders
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (order == null)
            return NotFound();

        if (order.DriverId != driver.Id)
        {
            return Forbid();
        }

        if (order.Status != OrderStatuses.DriverAssigned)
        {
            return BadRequest(
                "This order is not assigned to this driver.");
        }

        order.Status = OrderStatuses.DriverAccepted;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await AddStatusHistory(
            id,
            OrderStatuses.DriverAccepted);

        await AddAuditLog(
            id,
            $"Driver {driver.Id} accepted order.");

        order.Status = OrderStatuses.WaitingForPickup;
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await AddStatusHistory(
            id,
            OrderStatuses.WaitingForPickup);

        await AddAuditLog(
            id,
            "Order is waiting for pickup.");
        return Ok(new
        {
            success = true,
            orderId = id,
            driverId = driver.Id,
            status = order.Status
        });
    }

    [HttpGet("my-assigned")]
    [Authorize(Roles = "supplier,provider,driver,admin,dispatcher")]
    public async Task<IActionResult> GetMyAssignedOrders(
    CancellationToken cancellationToken)
    {
        Guid userId;

        try
        {
            userId = User.GetUserId();
        }
        catch
        {
            return Unauthorized();
        }

        var role = User.GetRole()
            .Trim()
            .ToLowerInvariant();

        Guid? supplierId = null;
        Guid? driverId = null;

        if (role == "supplier" || role == "provider")
        {
            var supplier = await _context.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.UserId == userId,
                    cancellationToken);

            if (supplier == null)
                return NotFound("Supplier profile not found.");

            supplierId = supplier.Id;
        }

        if (role == "driver")
        {
            var driver = await _context.Drivers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.UserId == userId,
                    cancellationToken);

            if (driver == null)
                return NotFound("Driver profile not found.");

            driverId = driver.Id;
        }

        var query = _context.Orders
            .AsNoTracking()
            .Include(x => x.Driver)
            .Include(x => x.Supplier)
            .AsQueryable();

        if (supplierId.HasValue)
        {
            query = query.Where(order =>
                order.SupplierId == supplierId.Value
                ||
                _context.OrderItems.Any(item =>
                    item.OrderId == order.Id &&
                    item.SupplierId == supplierId.Value
                ));
        }

        if (driverId.HasValue)
        {
            query = query.Where(
                order => order.DriverId == driverId.Value);
        }

        var orders = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(order => new
            {
                id = order.Id,
                orderNumber = order.OrderNumber,
                customerName = order.CustomerName,
                pickupAddress = order.PickupAddress,
                deliveryAddress = order.DeliveryAddress,
                zone = order.Zone,
                status = order.Status,
                createdAt = order.CreatedAt,
                updatedAt = order.UpdatedAt,

                supplierId = order.SupplierId,
                driverId = order.DriverId,

                items = _context.OrderItems
                    .Where(item =>
                        item.OrderId == order.Id &&
                        (
                            !supplierId.HasValue ||
                            item.SupplierId == supplierId.Value
                        ))
                    .Select(item => new
                    {
                        item.Id,
                        item.ProductId,
                        item.SupplierId,
                        item.Quantity,
                        item.UnitPrice,
                        productName = item.Product.Name
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return Ok(orders);
    }

    [HttpGet("my-supplier-orders")]
    [Authorize(Roles = "supplier,provider")]
    public async Task<IActionResult> GetMySupplierOrders(
    CancellationToken cancellationToken)
    {
        Guid userId;

        try
        {
            userId = User.GetUserId();
        }
        catch
        {
            return Unauthorized();
        }

        var supplier = await _context.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId,
                cancellationToken);

        if (supplier == null)
            return NotFound("Supplier profile not found.");

        var orders = await _context.Orders
            .AsNoTracking()
            .Where(order =>
                _context.OrderItems.Any(item =>
                    item.OrderId == order.Id &&
                    item.SupplierId == supplier.Id
                ))
            .OrderByDescending(order => order.CreatedAt)
            .Select(order => new
            {
                id = order.Id,
                orderNumber = order.OrderNumber,
                customerName = order.CustomerName,
                pickupAddress = order.PickupAddress,
                deliveryAddress = order.DeliveryAddress,
                itemDescription = order.ItemDescription,
                status = order.Status,
                createdAt = order.CreatedAt,

                items = _context.OrderItems
        .Where(item =>
            item.OrderId == order.Id &&
            item.SupplierId == supplier.Id)
        .Select(item => new
        {
            item.Id,
            item.ProductId,
            item.SupplierId,
            item.Quantity,
            item.UnitPrice,
            productName = item.Product.Name
        })
        .ToList()
            })
            .ToListAsync(cancellationToken);

        return Ok(orders);
    }

    [HttpGet("my-driver-orders")]
    [Authorize(Roles = "driver")]
    public async Task<IActionResult> GetMyDriverOrders(
    CancellationToken cancellationToken)
    {
        Guid userId;

        try
        {
            userId = User.GetUserId();
        }
        catch
        {
            return Unauthorized();
        }

        // Get the authenticated user's email as a fallback.
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken);

        if (user == null)
            return Unauthorized();

        // Primary lookup: drivers.user_id
        // Fallback: drivers.email
        var driver = await _context.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.UserId == userId ||
                    (
                        x.UserId == null &&
                        x.Email != null &&
                        x.Email.ToLower() == user.Email.ToLower()
                    ),
                cancellationToken);

        if (driver == null)
        {
            return NotFound(new
            {
                message = "Driver profile not found.",
                userId
            });
        }

        var orders = await _context.Orders
            .AsNoTracking()
            .Where(x => x.DriverId == driver.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                id = x.Id,
                orderNumber = x.OrderNumber,

                customerName = x.CustomerName,

                pickupAddress = x.PickupAddress,
                deliveryAddress = x.DeliveryAddress,

                itemDescription = x.ItemDescription,

                zone = x.Zone,

                supplierId = x.SupplierId,

                // IMPORTANT:
                // This was missing from your original response.
                driverId = x.DriverId,

                status = x.Status,

                createdAt = x.CreatedAt,
                updatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(orders);
    }

    private async Task<Driver?> GetCurrentDriver(
    CancellationToken cancellationToken)
    {
        Guid userId;

        try
        {
            userId = User.GetUserId();
        }
        catch
        {
            return null;
        }

        return await _context.Drivers
            .FirstOrDefaultAsync(
                x => x.UserId == userId,
                cancellationToken);
    }

    private async Task<Order?> GetDriverOrder(
    Guid orderId,
    Guid driverId,
    CancellationToken cancellationToken)
    {
        return await _context.Orders
            .FirstOrDefaultAsync(
                o =>
                    o.Id == orderId &&
                    o.DriverId == driverId,
                cancellationToken);
    }


    private async Task AddOrderStatusHistory(
        Guid orderId,
        string status,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        _context.StatusHistory.Add(
            new StatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                Status = status,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            });

        await _context.SaveChangesAsync(
            cancellationToken);
    }


    private async Task AddOrderAuditLog(
        Guid orderId,
        string action,
        CancellationToken cancellationToken = default)
    {
        _context.AuditLogs.Add(
            new AuditLog
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                Action = action,
                PerformedBy =
                    User.Identity?.Name ?? "system",
                CreatedAt = DateTime.UtcNow
            });

        await _context.SaveChangesAsync(
            cancellationToken);
    }
}
