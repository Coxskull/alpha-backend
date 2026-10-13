using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alpha.API.Migrations
{
    /// <inheritdoc />
    public partial class ReproduceModelAndPaymentOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_drivers_driver_id",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_suppliers_supplier_id",
                table: "orders");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "referral_code",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "referral_joined_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "referred_by_user_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentWorkload",
                table: "suppliers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "ResponseRate",
                table: "suppliers",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "Territory",
                table: "suppliers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "suppliers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "customer_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActiveJobs",
                table: "drivers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "drivers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ResponseRate",
                table: "drivers",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "Territory",
                table: "drivers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "drivers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                table: "drivers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "drivers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "auto_parts_commission_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: true),
                    policy_version = table.Column<int>(type: "integer", nullable: true),
                    action = table.Column<string>(type: "text", nullable: false),
                    performed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    before_snapshot = table.Column<string>(type: "text", nullable: true),
                    after_snapshot = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auto_parts_commission_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_auto_parts_commission_audit_logs_users_performed_by_user_id",
                        column: x => x.performed_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "auto_parts_commission_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_name = table.Column<string>(type: "text", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auto_parts_commission_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cart_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cart_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customer_addresses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    zip_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_addresses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customer_vehicles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    make = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Trim = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    engine = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VIN = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_vehicles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "driver_payouts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    payout_status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_payouts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "entrepreneur_earning_adjustments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entrepreneur_earning_id = table.Column<Guid>(type: "uuid", nullable: false),
                    adjustment_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    related_payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entrepreneur_earning_adjustments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "entrepreneur_market_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    commission_rate = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    minimum_payout_threshold = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entrepreneur_market_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "entrepreneur_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    city = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: true),
                    country = table.Column<string>(type: "text", nullable: false),
                    preferred_language = table.Column<string>(type: "text", nullable: false),
                    business_name = table.Column<string>(type: "text", nullable: true),
                    entrepreneurial_goal = table.Column<string>(type: "text", nullable: true),
                    onboarding_status = table.Column<string>(type: "text", nullable: false),
                    terms_accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rewards_policy_accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entrepreneur_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "entrepreneur_program_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    default_commission_rate = table.Column<decimal>(type: "numeric", nullable: false),
                    minimum_payout_threshold = table.Column<decimal>(type: "numeric", nullable: false),
                    payout_frequency = table.Column<string>(type: "text", nullable: false),
                    qualifying_provider_roles = table.Column<string>(type: "text", nullable: false),
                    qualifying_transaction_types = table.Column<string>(type: "text", nullable: false),
                    holding_period_days = table.Column<int>(type: "integer", nullable: false),
                    program_start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    program_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    maximum_referral_level = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entrepreneur_program_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "entrepreneur_referrals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entrepreneur_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recruited_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referral_code = table.Column<string>(type: "text", nullable: false),
                    referral_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    provider_activation_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    referral_status = table.Column<string>(type: "text", nullable: false),
                    eligibility_status = table.Column<string>(type: "text", nullable: false),
                    is_direct_referral = table.Column<bool>(type: "boolean", nullable: false),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    entrepreneur_eligibility_status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entrepreneur_referrals", x => x.id);
                    table.ForeignKey(
                        name: "FK_entrepreneur_referrals_users_entrepreneur_user_id",
                        column: x => x.entrepreneur_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_entrepreneur_referrals_users_recruited_user_id",
                        column: x => x.recruited_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "entrepreneur_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_key = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    icon = table.Column<string>(type: "text", nullable: true),
                    is_operational_role = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entrepreneur_roles", x => x.id);
                    table.UniqueConstraint("AK_entrepreneur_roles_role_key", x => x.role_key);
                });

            migrationBuilder.CreateTable(
                name: "entrepreneur_transaction_costs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cost_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entrepreneur_transaction_costs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "EntrepreneurPayoutBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    EarningCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    PayoutReference = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntrepreneurPayoutBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntrepreneurReferralAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntrepreneurReferralId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldEntrepreneurUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    NewEntrepreneurUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntrepreneurReferralAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    invoice_number = table.Column<string>(type: "text", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric", nullable: false),
                    tax = table.Column<decimal>(type: "numeric", nullable: false),
                    total = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mechanics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    service_area = table.Column<string>(type: "text", nullable: true),
                    availability_status = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric", nullable: true),
                    longitude = table.Column<decimal>(type: "numeric", nullable: true),
                    service_radius_km = table.Column<decimal>(type: "numeric", nullable: false),
                    active_jobs = table.Column<int>(type: "integer", nullable: false),
                    response_rate = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mechanics", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operational_alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_type = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    resolved = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_alerts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "order_financials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    currency = table.Column<string>(type: "text", nullable: false),
                    exchange_rate = table.Column<decimal>(type: "numeric", nullable: false),
                    item_subtotal = table.Column<decimal>(type: "numeric", nullable: false),
                    delivery_fee = table.Column<decimal>(type: "numeric", nullable: false),
                    service_fee = table.Column<decimal>(type: "numeric", nullable: false),
                    tax = table.Column<decimal>(type: "numeric", nullable: false),
                    discount = table.Column<decimal>(type: "numeric", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    customer_paid = table.Column<decimal>(type: "numeric", nullable: false),
                    supplier_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    driver_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    mechanic_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    alpha_platform_fee = table.Column<decimal>(type: "numeric", nullable: false),
                    supplier_earning = table.Column<decimal>(type: "numeric", nullable: false),
                    driver_earning = table.Column<decimal>(type: "numeric", nullable: false),
                    company_revenue = table.Column<decimal>(type: "numeric", nullable: false),
                    financial_status = table.Column<string>(type: "text", nullable: false),
                    payout_status = table.Column<string>(type: "text", nullable: false),
                    completion_proof_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    settlement_status = table.Column<string>(type: "text", nullable: false),
                    tax_collected = table.Column<decimal>(type: "numeric", nullable: false),
                    tax_withheld = table.Column<decimal>(type: "numeric", nullable: false),
                    processing_fee = table.Column<decimal>(type: "numeric", nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    dispute_reserve = table.Column<decimal>(type: "numeric", nullable: false),
                    supplier_net_payable = table.Column<decimal>(type: "numeric", nullable: false),
                    driver_net_payable = table.Column<decimal>(type: "numeric", nullable: false),
                    mechanic_net_payable = table.Column<decimal>(type: "numeric", nullable: false),
                    alpha_net_revenue = table.Column<decimal>(type: "numeric", nullable: false),
                    reconciliation_difference = table.Column<decimal>(type: "numeric", nullable: false),
                    auto_parts_commission = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    auto_parts_commission_rate = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    auto_parts_commission_policy_id = table.Column<Guid>(type: "uuid", nullable: true),
                    auto_parts_commission_policy_version = table.Column<int>(type: "integer", nullable: true),
                    parts_supplier_gross = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    parts_supplier_net = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    alpha_gross_parts_commission = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    alpha_gross_mechanic_commission = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    alpha_gross_delivery_commission = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    alpha_gross_platform_commission = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    direct_transaction_costs = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    alpha_eligible_net_platform_revenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    entrepreneur_commission = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    alpha_retained_revenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    chargeback_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    chargeback_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    payment_status = table.Column<string>(type: "text", nullable: false),
                    provider_payout_status = table.Column<string>(type: "text", nullable: false),
                    entrepreneur_payout_status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_financials", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parts_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mechanic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    part_description = table.Column<string>(type: "text", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parts_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_password_reset_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_webhook_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gateway = table.Column<string>(type: "text", nullable: false),
                    gateway_event_id = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    processed = table.Column<bool>(type: "boolean", nullable: false),
                    processing_error = table.Column<string>(type: "text", nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processing_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_webhook_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    payment_method = table.Column<string>(type: "text", nullable: false),
                    payment_status = table.Column<string>(type: "text", nullable: false),
                    transaction_reference = table.Column<string>(type: "text", nullable: true),
                    payment_gateway = table.Column<string>(type: "text", nullable: true),
                    gateway_checkout_session_id = table.Column<string>(type: "text", nullable: true),
                    gateway_payment_id = table.Column<string>(type: "text", nullable: true),
                    checkout_url = table.Column<string>(type: "text", nullable: true),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    gateway_fee = table.Column<decimal>(type: "numeric", nullable: false),
                    gateway_response = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    refunded_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    refund_status = table.Column<string>(type: "text", nullable: false),
                    refund_reference = table.Column<string>(type: "text", nullable: true),
                    refunded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    part_number = table.Column<string>(type: "text", nullable: false),
                    brand = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    image_url = table.Column<string>(type: "text", nullable: false),
                    price = table.Column<decimal>(type: "numeric", nullable: false),
                    quantity_available = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    low_stock_threshold = table.Column<int>(type: "integer", nullable: false),
                    image_base64 = table.Column<string>(type: "text", nullable: true),
                    currency = table.Column<string>(type: "text", nullable: false),
                    country_code = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "referral_commission_rates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_type = table.Column<string>(type: "text", nullable: false),
                    source_role = table.Column<string>(type: "text", nullable: false),
                    rate = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    fixed_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    currency = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referral_commission_rates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "referral_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    setting_key = table.Column<string>(type: "text", nullable: false),
                    setting_value = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referral_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "referral_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    beneficiary_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transaction_type = table.Column<string>(type: "text", nullable: false),
                    source_role = table.Column<string>(type: "text", nullable: true),
                    source_description = table.Column<string>(type: "text", nullable: true),
                    gross_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    commission_rate = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    commission_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    referral_level = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    available_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    metadata = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    event_key = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    eligible_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referral_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_referral_transactions_users_beneficiary_user_id",
                        column: x => x.beneficiary_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_referral_transactions_users_source_user_id",
                        column: x => x.source_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "repair_proofs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mechanic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repair_proofs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_verification_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_key = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    legal_name = table.Column<string>(type: "text", nullable: true),
                    business_name = table.Column<string>(type: "text", nullable: true),
                    identification_number = table.Column<string>(type: "text", nullable: true),
                    license_number = table.Column<string>(type: "text", nullable: true),
                    vehicle_plate_number = table.Column<string>(type: "text", nullable: true),
                    years_of_experience = table.Column<int>(type: "integer", nullable: true),
                    business_address = table.Column<string>(type: "text", nullable: true),
                    applicant_notes = table.Column<string>(type: "text", nullable: true),
                    reviewer_notes = table.Column<string>(type: "text", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_verification_applications", x => x.id);
                    table.ForeignKey(
                        name: "FK_role_verification_applications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "security_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    role = table.Column<string>(type: "text", nullable: true),
                    path = table.Column<string>(type: "text", nullable: false),
                    method = table.Column<string>(type: "text", nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    message = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "settlement_queue",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_financial_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payee_type = table.Column<string>(type: "text", nullable: false),
                    payee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    reviewed_by = table.Column<string>(type: "text", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settlement_queue", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_payouts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    payout_status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_payouts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tax_calculations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    country = table.Column<string>(type: "text", nullable: false),
                    region = table.Column<string>(type: "text", nullable: true),
                    currency = table.Column<string>(type: "text", nullable: false),
                    component = table.Column<string>(type: "text", nullable: false),
                    tax_type = table.Column<string>(type: "text", nullable: false),
                    tax_rate = table.Column<decimal>(type: "numeric", nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    revenue_recipient = table.Column<string>(type: "text", nullable: false),
                    tax_responsible_party = table.Column<string>(type: "text", nullable: false),
                    withholding_required = table.Column<bool>(type: "boolean", nullable: false),
                    withholding_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    tax_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_rule_version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_calculations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tax_ledger_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    settlement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entry_type = table.Column<string>(type: "text", nullable: false, defaultValue: "calculation"),
                    tax_type = table.Column<string>(type: "text", nullable: false),
                    component = table.Column<string>(type: "text", nullable: false),
                    tax_rate = table.Column<decimal>(type: "numeric", nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric", nullable: false),
                    tax_collected = table.Column<decimal>(type: "numeric", nullable: false),
                    tax_withheld = table.Column<decimal>(type: "numeric", nullable: false),
                    tax_refunded = table.Column<decimal>(type: "numeric", nullable: false),
                    responsible_party = table.Column<string>(type: "text", nullable: false),
                    tax_rule_version = table.Column<int>(type: "integer", nullable: false),
                    actor = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_ledger_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tax_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    country = table.Column<string>(type: "text", nullable: false),
                    region = table.Column<string>(type: "text", nullable: true),
                    tax_type = table.Column<string>(type: "text", nullable: false),
                    tax_rate = table.Column<decimal>(type: "numeric", nullable: false),
                    component = table.Column<string>(type: "text", nullable: false),
                    responsible_party = table.Column<string>(type: "text", nullable: false),
                    is_tax_inclusive = table.Column<bool>(type: "boolean", nullable: false),
                    withholding_required = table.Column<bool>(type: "boolean", nullable: false),
                    withholding_rate = table.Column<decimal>(type: "numeric", nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "auto_parts_commission_calculations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_financial_id = table.Column<Guid>(type: "uuid", nullable: true),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version = table.Column<int>(type: "integer", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    parts_subtotal = table.Column<decimal>(type: "numeric", nullable: false),
                    total_commission = table.Column<decimal>(type: "numeric", nullable: false),
                    effective_commission_rate = table.Column<decimal>(type: "numeric", nullable: false),
                    calculated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auto_parts_commission_calculations", x => x.id);
                    table.ForeignKey(
                        name: "FK_auto_parts_commission_calculations_auto_parts_commission_po~",
                        column: x => x.policy_id,
                        principalTable: "auto_parts_commission_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "auto_parts_commission_tiers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier_order = table.Column<int>(type: "integer", nullable: false),
                    minimum_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    maximum_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    commission_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auto_parts_commission_tiers", x => x.id);
                    table.ForeignKey(
                        name: "FK_auto_parts_commission_tiers_auto_parts_commission_policies_~",
                        column: x => x.policy_id,
                        principalTable: "auto_parts_commission_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_key = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    activated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_roles_entrepreneur_roles_role_key",
                        column: x => x.role_key,
                        principalTable: "entrepreneur_roles",
                        principalColumn: "role_key",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_name = table.Column<string>(type: "text", nullable: false),
                    customer_phone = table.Column<string>(type: "text", nullable: true),
                    vehicle_info = table.Column<string>(type: "text", nullable: true),
                    issue_description = table.Column<string>(type: "text", nullable: false),
                    service_address = table.Column<string>(type: "text", nullable: false),
                    zone = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric", nullable: true),
                    longitude = table.Column<decimal>(type: "numeric", nullable: true),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mechanic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parts_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    parts_request_note = table.Column<string>(type: "text", nullable: true),
                    proof_image_url = table.Column<string>(type: "text", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    final_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    payment_status = table.Column<string>(type: "text", nullable: false),
                    provider_accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    mechanic_accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    driver_assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    proof_uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_requests_drivers_driver_id",
                        column: x => x.driver_id,
                        principalTable: "drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_service_requests_mechanics_mechanic_id",
                        column: x => x.mechanic_id,
                        principalTable: "mechanics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_service_requests_suppliers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "entrepreneur_earnings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entrepreneur_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recruiter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recruited_provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_role = table.Column<string>(type: "text", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<string>(type: "text", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transaction_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    alpha_gross_platform_commission = table.Column<decimal>(type: "numeric", nullable: false),
                    direct_transaction_costs = table.Column<decimal>(type: "numeric", nullable: false),
                    eligible_net_platform_revenue = table.Column<decimal>(type: "numeric", nullable: false),
                    entrepreneur_percentage = table.Column<decimal>(type: "numeric", nullable: false),
                    entrepreneur_earnings_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    earning_status = table.Column<string>(type: "text", nullable: false),
                    refund_adjustment = table.Column<decimal>(type: "numeric", nullable: false),
                    chargeback_adjustment = table.Column<decimal>(type: "numeric", nullable: false),
                    payout_batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payout_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payout_reference = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    event_key = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entrepreneur_earnings", x => x.id);
                    table.ForeignKey(
                        name: "FK_entrepreneur_earnings_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_entrepreneur_earnings_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_entrepreneur_earnings_users_entrepreneur_user_id",
                        column: x => x.entrepreneur_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    provider_request_id = table.Column<string>(type: "character varying(38)", maxLength: 38, nullable: false),
                    provider_reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    provider_parent_reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_operations", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_operations_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_items_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_items_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_verification_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "text", nullable: false),
                    original_file_name = table.Column<string>(type: "text", nullable: false),
                    storage_path = table.Column<string>(type: "text", nullable: false),
                    file_path = table.Column<string>(type: "text", nullable: true),
                    content_type = table.Column<string>(type: "text", nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    verification_status = table.Column<string>(type: "text", nullable: false, defaultValue: "pending"),
                    reviewer_notes = table.Column<string>(type: "text", nullable: true),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_verification_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_role_verification_documents_role_verification_applications_~",
                        column: x => x.application_id,
                        principalTable: "role_verification_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "auto_parts_commission_calculation_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    calculation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier_order = table.Column<int>(type: "integer", nullable: false),
                    tier_minimum = table.Column<decimal>(type: "numeric", nullable: false),
                    tier_maximum = table.Column<decimal>(type: "numeric", nullable: true),
                    tier_percentage = table.Column<decimal>(type: "numeric", nullable: false),
                    amount_in_tier = table.Column<decimal>(type: "numeric", nullable: false),
                    commission_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auto_parts_commission_calculation_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_auto_parts_commission_calculation_lines_auto_parts_commissi~",
                        column: x => x.calculation_id,
                        principalTable: "auto_parts_commission_calculations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_auto_parts_commission_calculation_lines_auto_parts_commiss~1",
                        column: x => x.tier_id,
                        principalTable: "auto_parts_commission_tiers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_referral_code",
                table: "users",
                column: "referral_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_referred_by_user_id",
                table: "users",
                column: "referred_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_customer_id",
                table: "orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_drivers_user_id",
                table: "drivers",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_proof_order_id",
                table: "delivery_proof",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_auto_parts_commission_audit_logs_performed_by_user_id",
                table: "auto_parts_commission_audit_logs",
                column: "performed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_auto_parts_commission_calculation_lines_calculation_id",
                table: "auto_parts_commission_calculation_lines",
                column: "calculation_id");

            migrationBuilder.CreateIndex(
                name: "IX_auto_parts_commission_calculation_lines_tier_id",
                table: "auto_parts_commission_calculation_lines",
                column: "tier_id");

            migrationBuilder.CreateIndex(
                name: "IX_auto_parts_commission_calculations_policy_id",
                table: "auto_parts_commission_calculations",
                column: "policy_id");

            migrationBuilder.CreateIndex(
                name: "IX_auto_parts_commission_tiers_policy_id_tier_order",
                table: "auto_parts_commission_tiers",
                columns: new[] { "policy_id", "tier_order" });

            migrationBuilder.CreateIndex(
                name: "IX_entrepreneur_earning_adjustments_entrepreneur_earning_id",
                table: "entrepreneur_earning_adjustments",
                column: "entrepreneur_earning_id");

            migrationBuilder.CreateIndex(
                name: "IX_entrepreneur_earnings_entrepreneur_user_id",
                table: "entrepreneur_earnings",
                column: "entrepreneur_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_entrepreneur_earnings_order_id",
                table: "entrepreneur_earnings",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_entrepreneur_earnings_payment_id",
                table: "entrepreneur_earnings",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_entrepreneur_market_configurations_country_code_currency",
                table: "entrepreneur_market_configurations",
                columns: new[] { "country_code", "currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entrepreneur_profiles_user_id",
                table: "entrepreneur_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entrepreneur_referrals_entrepreneur_user_id",
                table: "entrepreneur_referrals",
                column: "entrepreneur_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_entrepreneur_referrals_recruited_user_id",
                table: "entrepreneur_referrals",
                column: "recruited_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_entrepreneur_transaction_costs_order_id",
                table: "entrepreneur_transaction_costs",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_order_id",
                table: "order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_product_id",
                table: "order_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_supplier_id",
                table: "order_items",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_token_hash",
                table: "password_reset_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_user_id",
                table: "password_reset_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_operations_kind_provider_reference",
                table: "payment_operations",
                columns: new[] { "kind", "provider_reference" },
                unique: true,
                filter: "provider_reference IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payment_operations_payment_id_kind_idempotency_key",
                table: "payment_operations",
                columns: new[] { "payment_id", "kind", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_operations_provider_request_id",
                table: "payment_operations",
                column: "provider_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_webhook_events_gateway_gateway_event_id",
                table: "payment_webhook_events",
                columns: new[] { "gateway", "gateway_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_referral_commission_rates_transaction_type_source_role_curr~",
                table: "referral_commission_rates",
                columns: new[] { "transaction_type", "source_role", "currency" });

            migrationBuilder.CreateIndex(
                name: "IX_referral_settings_setting_key",
                table: "referral_settings",
                column: "setting_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_referral_transactions_beneficiary_user_id",
                table: "referral_transactions",
                column: "beneficiary_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_referral_transactions_event_key",
                table: "referral_transactions",
                column: "event_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_referral_transactions_order_id",
                table: "referral_transactions",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_referral_transactions_source_user_id",
                table: "referral_transactions",
                column: "source_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_verification_applications_user_id_role_key",
                table: "role_verification_applications",
                columns: new[] { "user_id", "role_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_verification_documents_application_id",
                table: "role_verification_documents",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_requests_driver_id",
                table: "service_requests",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_requests_mechanic_id",
                table: "service_requests",
                column: "mechanic_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_requests_provider_id",
                table: "service_requests",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "IX_tax_calculations_order_id_component_tax_type",
                table: "tax_calculations",
                columns: new[] { "order_id", "component", "tax_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role_key",
                table: "user_roles",
                column: "role_key");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_user_id_role_key",
                table: "user_roles",
                columns: new[] { "user_id", "role_key" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_delivery_proof_orders_order_id",
                table: "delivery_proof",
                column: "order_id",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_drivers_users_user_id",
                table: "drivers",
                column: "user_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_drivers_driver_id",
                table: "orders",
                column: "driver_id",
                principalTable: "drivers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_suppliers_supplier_id",
                table: "orders",
                column: "supplier_id",
                principalTable: "suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_users_customer_id",
                table: "orders",
                column: "customer_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_users_users_referred_by_user_id",
                table: "users",
                column: "referred_by_user_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_delivery_proof_orders_order_id",
                table: "delivery_proof");

            migrationBuilder.DropForeignKey(
                name: "FK_drivers_users_user_id",
                table: "drivers");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_drivers_driver_id",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_suppliers_supplier_id",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_users_customer_id",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_users_users_referred_by_user_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "auto_parts_commission_audit_logs");

            migrationBuilder.DropTable(
                name: "auto_parts_commission_calculation_lines");

            migrationBuilder.DropTable(
                name: "cart_items");

            migrationBuilder.DropTable(
                name: "customer_addresses");

            migrationBuilder.DropTable(
                name: "customer_vehicles");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "driver_payouts");

            migrationBuilder.DropTable(
                name: "entrepreneur_earning_adjustments");

            migrationBuilder.DropTable(
                name: "entrepreneur_earnings");

            migrationBuilder.DropTable(
                name: "entrepreneur_market_configurations");

            migrationBuilder.DropTable(
                name: "entrepreneur_profiles");

            migrationBuilder.DropTable(
                name: "entrepreneur_program_configurations");

            migrationBuilder.DropTable(
                name: "entrepreneur_referrals");

            migrationBuilder.DropTable(
                name: "entrepreneur_transaction_costs");

            migrationBuilder.DropTable(
                name: "EntrepreneurPayoutBatches");

            migrationBuilder.DropTable(
                name: "EntrepreneurReferralAudits");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "operational_alerts");

            migrationBuilder.DropTable(
                name: "order_financials");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "parts_requests");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "payment_operations");

            migrationBuilder.DropTable(
                name: "payment_webhook_events");

            migrationBuilder.DropTable(
                name: "referral_commission_rates");

            migrationBuilder.DropTable(
                name: "referral_settings");

            migrationBuilder.DropTable(
                name: "referral_transactions");

            migrationBuilder.DropTable(
                name: "repair_proofs");

            migrationBuilder.DropTable(
                name: "role_verification_documents");

            migrationBuilder.DropTable(
                name: "security_logs");

            migrationBuilder.DropTable(
                name: "service_requests");

            migrationBuilder.DropTable(
                name: "settlement_queue");

            migrationBuilder.DropTable(
                name: "supplier_payouts");

            migrationBuilder.DropTable(
                name: "tax_calculations");

            migrationBuilder.DropTable(
                name: "tax_ledger_entries");

            migrationBuilder.DropTable(
                name: "tax_rules");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "auto_parts_commission_calculations");

            migrationBuilder.DropTable(
                name: "auto_parts_commission_tiers");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "role_verification_applications");

            migrationBuilder.DropTable(
                name: "mechanics");

            migrationBuilder.DropTable(
                name: "entrepreneur_roles");

            migrationBuilder.DropTable(
                name: "auto_parts_commission_policies");

            migrationBuilder.DropIndex(
                name: "IX_users_referral_code",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_referred_by_user_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_orders_customer_id",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_drivers_user_id",
                table: "drivers");

            migrationBuilder.DropIndex(
                name: "IX_delivery_proof_order_id",
                table: "delivery_proof");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "referral_code",
                table: "users");

            migrationBuilder.DropColumn(
                name: "referral_joined_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "referred_by_user_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "CurrentWorkload",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "ResponseRate",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "Territory",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "customer_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ActiveJobs",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "ResponseRate",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "Territory",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "email",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "password_hash",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "drivers");

            migrationBuilder.AddForeignKey(
                name: "FK_orders_drivers_driver_id",
                table: "orders",
                column: "driver_id",
                principalTable: "drivers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_orders_suppliers_supplier_id",
                table: "orders",
                column: "supplier_id",
                principalTable: "suppliers",
                principalColumn: "Id");
        }
    }
}
