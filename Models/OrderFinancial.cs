using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("order_financials")]
public class OrderFinancial
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_id")]
    public Guid? OrderId { get; set; }

    [Column("service_request_id")]
    public Guid? ServiceRequestId { get; set; }

    [Column("currency")]
    public string Currency { get; set; } = "USD";

    [Column("exchange_rate")]
    public decimal ExchangeRate { get; set; } = 1;

    [Column("item_subtotal")]
    public decimal ItemSubtotal { get; set; }

    [Column("delivery_fee")]
    public decimal DeliveryFee { get; set; }

    [Column("service_fee")]
    public decimal ServiceFee { get; set; }

    [Column("tax")]
    public decimal Tax { get; set; }

    [Column("discount")]
    public decimal Discount { get; set; }

    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [Column("customer_paid")]
    public decimal CustomerPaid { get; set; }

    [Column("supplier_amount")]
    public decimal SupplierAmount { get; set; }

    [Column("driver_amount")]
    public decimal DriverAmount { get; set; }

    [Column("mechanic_amount")]
    public decimal MechanicAmount { get; set; }

    [Column("alpha_platform_fee")]
    public decimal AlphaPlatformFee { get; set; }

    [Column("supplier_earning")]
    public decimal SupplierEarning { get; set; }

    [Column("driver_earning")]
    public decimal DriverEarning { get; set; }

    [Column("company_revenue")]
    public decimal CompanyRevenue { get; set; }

    [Column("financial_status")]
    public string FinancialStatus { get; set; } = "pending_review";

    [Column("payout_status")]
    public string PayoutStatus { get; set; } = "manual_review";

    [Column("completion_proof_url")]
    public string? CompletionProofUrl { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("settlement_status")]
    public string SettlementStatus { get; set; } = "pending";

    [Column("tax_collected")]
    public decimal TaxCollected { get; set; }

    [Column("tax_withheld")]
    public decimal TaxWithheld { get; set; }

    [Column("processing_fee")]
    public decimal ProcessingFee { get; set; }

    [Column("refund_amount")]
    public decimal RefundAmount { get; set; }

    [Column("dispute_reserve")]
    public decimal DisputeReserve { get; set; }

    [Column("supplier_net_payable")]
    public decimal SupplierNetPayable { get; set; }

    [Column("driver_net_payable")]
    public decimal DriverNetPayable { get; set; }

    [Column("mechanic_net_payable")]
    public decimal MechanicNetPayable { get; set; }

    [Column("alpha_net_revenue")]
    public decimal AlphaNetRevenue { get; set; }

    [Column("reconciliation_difference")]
    public decimal ReconciliationDifference { get; set; }

    // =====================================================
    // AUTO-PARTS MARKETPLACE COMMISSION
    // =====================================================

    public decimal AutoPartsCommission { get; set; }

    public decimal AutoPartsCommissionRate { get; set; }

    public Guid? AutoPartsCommissionPolicyId { get; set; }

    public int? AutoPartsCommissionPolicyVersion { get; set; }

    public decimal PartsSupplierGross { get; set; }

    public decimal PartsSupplierNet { get; set; }

    // =====================================================
    // MARKETPLACE COMMISSION
    // =====================================================

    [Column("auto_parts_commission")]
    public decimal AutoPartsCommission { get; set; }

    [Column("auto_parts_commission_rate")]
    public decimal AutoPartsCommissionRate { get; set; }

    [Column("auto_parts_commission_policy_id")]
    public Guid? AutoPartsCommissionPolicyId { get; set; }

    [Column("auto_parts_commission_policy_version")]
    public int? AutoPartsCommissionPolicyVersion { get; set; }

    [Column("parts_supplier_gross")]
    public decimal PartsSupplierGross { get; set; }

    [Column("parts_supplier_net")]
    public decimal PartsSupplierNet { get; set; }

    [Column("alpha_gross_parts_commission")]
    public decimal AlphaGrossPartsCommission { get; set; }

    [Column("alpha_gross_mechanic_commission")]
    public decimal AlphaGrossMechanicCommission { get; set; }

    [Column("alpha_gross_delivery_commission")]
    public decimal AlphaGrossDeliveryCommission { get; set; }

    [Column("alpha_gross_platform_commission")]
    public decimal AlphaGrossPlatformCommission { get; set; }

    // =====================================================
    // ENTREPRENEUR ECONOMICS
    // =====================================================

    [Column("direct_transaction_costs")]
    public decimal DirectTransactionCosts { get; set; }

    [Column("alpha_eligible_net_platform_revenue")]
    public decimal AlphaEligibleNetPlatformRevenue { get; set; }

    [Column("entrepreneur_commission")]
    public decimal EntrepreneurCommission { get; set; }

    [Column("alpha_retained_revenue")]
    public decimal AlphaRetainedRevenue { get; set; }

    // =====================================================
    // REFUNDS / CHARGEBACKS
    // =====================================================

    [Column("chargeback_amount")]
    public decimal ChargebackAmount { get; set; }

    [Column("chargeback_fee")]
    public decimal ChargebackFee { get; set; }

    // =====================================================
    // STATUS
    // =====================================================

    [Column("payment_status")]
    public string PaymentStatus { get; set; } = "pending";

    [Column("provider_payout_status")]
    public string ProviderPayoutStatus { get; set; } = "pending";

    [Column("entrepreneur_payout_status")]
    public string EntrepreneurPayoutStatus { get; set; } = "pending";


}