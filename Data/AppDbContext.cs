using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Alpha.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // Core Tables
    public DbSet<User> Users { get; set; }
    public DbSet<Order> Orders { get; set; }

    // Operations
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<Driver> Drivers { get; set; }
    public DbSet<StatusHistory> StatusHistory { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<DeliveryProof> DeliveryProofs { get; set; }
    public DbSet<OperationalAlert> OperationalAlerts { get; set; }

    // Customer Module
    public DbSet<Product> Products { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<CustomerAddress> CustomerAddresses { get; set; }
    public DbSet<CustomerVehicle> CustomerVehicles { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    // Mechanic Module
    public DbSet<Mechanic> Mechanics { get; set; }
    public DbSet<ServiceRequest> ServiceRequests { get; set; }
    public DbSet<PartsRequest> PartsRequests { get; set; }
    public DbSet<RepairProof> RepairProofs { get; set; }
    public DbSet<SecurityLog> SecurityLogs { get; set; }

    // Financial Module
    public DbSet<OrderFinancial> OrderFinancials { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<SettlementQueue> SettlementQueue { get; set; }
    public DbSet<Invoice> Invoices { get; set; }


    public DbSet<TaxRule> TaxRules { get; set; }
    public DbSet<TaxCalculation> TaxCalculations { get; set; }
    public DbSet<TaxLedgerEntry> TaxLedgerEntries { get; set; }

    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
    public DbSet<ReferralTransaction> ReferralTransactions =>
    Set<ReferralTransaction>();

    public DbSet<ReferralSetting> ReferralSettings { get; set; }
    public DbSet<EntrepreneurRole> EntrepreneurRoles { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<EntrepreneurProfile> EntrepreneurProfiles { get; set; }

    public DbSet<ReferralCommissionRate> ReferralCommissionRates =>
    Set<ReferralCommissionRate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ==========================
        // USERS
        // ==========================

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(user => user.Id);

            entity.Property(user => user.Id)
                .HasColumnName("Id");

            entity.Property(user => user.FullName)
                .HasColumnName("FullName");

            entity.Property(user => user.Email)
                .HasColumnName("Email");

            entity.Property(user => user.Phone)
                .HasColumnName("Phone");

            entity.Property(user => user.Role)
                .HasColumnName("Role");

            entity.Property(user => user.CreatedAt)
                .HasColumnName("CreatedAt");

            entity.Property(user => user.PasswordHash)
                .HasColumnName("PasswordHash");

            entity.Property(user => user.IsActive)
                .HasColumnName("IsActive");

            entity.Property(user => user.ReferralCode)
                .HasColumnName("referral_code");

            entity.Property(user => user.ReferredByUserId)
                .HasColumnName("referred_by_user_id");

            entity.Property(user => user.ReferralJoinedAt)
                .HasColumnName("referral_joined_at");

            entity.HasIndex(user => user.ReferralCode)
                .IsUnique();

            entity.HasOne(user => user.ReferredByUser)
                .WithMany(user => user.DirectReferrals)
                .HasForeignKey(user => user.ReferredByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<UserRole>()
    .HasIndex(x => new { x.UserId, x.RoleKey })
    .IsUnique();

        modelBuilder.Entity<UserRole>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserRole>()
            .HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleKey)
            .HasPrincipalKey(x => x.RoleKey);

        modelBuilder.Entity<ReferralCommissionRate>(entity =>
        {
            entity.ToTable("referral_commission_rates");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.TransactionType)
                .HasColumnName("transaction_type");

            entity.Property(x => x.SourceRole)
                .HasColumnName("source_role");

            entity.Property(x => x.Rate)
                .HasColumnName("rate")
                .HasPrecision(10, 6);

            entity.Property(x => x.FixedAmount)
                .HasColumnName("fixed_amount")
                .HasPrecision(18, 2);

            entity.Property(x => x.Currency)
                .HasColumnName("currency");

            entity.Property(x => x.IsActive)
                .HasColumnName("is_active");

            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at");

            entity.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at");

            entity.HasIndex(x => new
            {
                x.TransactionType,
                x.SourceRole,
                x.Currency
            });
        });

        modelBuilder.Entity<ReferralTransaction>(entity =>
        {
            entity.ToTable("referral_transactions");

            entity.HasKey(transaction => transaction.Id);

            entity.Property(transaction => transaction.Id)
                .HasColumnName("id");

            entity.Property(transaction => transaction.BeneficiaryUserId)
                .HasColumnName("beneficiary_user_id")
                .IsRequired();

            entity.Property(transaction => transaction.SourceUserId)
                .HasColumnName("source_user_id")
                .IsRequired();

            entity.Property(transaction => transaction.OrderId)
                .HasColumnName("order_id");

            entity.Property(transaction => transaction.ServiceRequestId)
                .HasColumnName("service_request_id");

            entity.Property(transaction => transaction.PaymentId)
                .HasColumnName("payment_id");

            entity.Property(transaction => transaction.TransactionType)
                .HasColumnName("transaction_type")
                .IsRequired();

            entity.Property(transaction => transaction.SourceRole)
                .HasColumnName("source_role");

            entity.Property(transaction => transaction.SourceDescription)
                .HasColumnName("source_description");

            entity.Property(transaction => transaction.GrossAmount)
                .HasColumnName("gross_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(transaction => transaction.CommissionRate)
                .HasColumnName("commission_rate")
                .HasPrecision(10, 6)
                .IsRequired();

            entity.Property(transaction => transaction.CommissionAmount)
                .HasColumnName("commission_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(transaction => transaction.Currency)
                .HasColumnName("currency")
                .IsRequired();

            entity.Property(transaction => transaction.ReferralLevel)
                .HasColumnName("referral_level")
                .IsRequired();

            entity.Property(transaction => transaction.Status)
                .HasColumnName("status")
                .IsRequired();

            entity.Property(transaction => transaction.AvailableAt)
                .HasColumnName("available_at");

            entity.Property(transaction => transaction.PaidAt)
                .HasColumnName("paid_at");

            entity.Property(transaction => transaction.Metadata)
                .HasColumnName("metadata")
                .HasColumnType("jsonb");

            entity.Property(transaction => transaction.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity.Property(transaction => transaction.EventKey)
                .HasColumnName("event_key");

            entity.Property(transaction => transaction.Description)
                .HasColumnName("description");

            entity.Property(transaction => transaction.EligibleAmount)
                .HasColumnName("eligible_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(transaction => transaction.ApprovedAt)
                .HasColumnName("approved_at");

            entity.HasOne(transaction => transaction.BeneficiaryUser)
                .WithMany(user => user.ReferralEarnings)
                .HasForeignKey(transaction => transaction.BeneficiaryUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(transaction => transaction.SourceUser)
                .WithMany(user => user.GeneratedReferralTransactions)
                .HasForeignKey(transaction => transaction.SourceUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(transaction => transaction.BeneficiaryUserId);

            entity.HasIndex(transaction => transaction.SourceUserId);

            entity.HasIndex(transaction => transaction.OrderId);

            entity.HasIndex(transaction => transaction.EventKey)
                .IsUnique();
        });

        modelBuilder.Entity<ReferralSetting>(entity =>
        {
            entity.ToTable("referral_settings");

            entity.HasKey(setting => setting.Id);

            entity.Property(setting => setting.Id)
                .HasColumnName("id");

            entity.Property(setting => setting.SettingKey)
                .HasColumnName("setting_key");

            entity.Property(setting => setting.SettingValue)
                .HasColumnName("setting_value");

            entity.Property(setting => setting.Description)
                .HasColumnName("description");

            entity.Property(setting => setting.UpdatedAt)
                .HasColumnName("updated_at");

            entity.HasIndex(setting => setting.SettingKey)
                .IsUnique();
        });

        modelBuilder.Entity<EntrepreneurProfile>(
    entity =>
    {
        entity.ToTable(
            "entrepreneur_profiles");

        entity.HasKey(profile =>
            profile.Id);

        entity.Property(profile =>
                profile.Id)
            .HasColumnName("id");

        entity.Property(profile =>
                profile.UserId)
            .HasColumnName("user_id");

        entity.Property(profile =>
                profile.City)
            .HasColumnName("city");

        entity.Property(profile =>
                profile.State)
            .HasColumnName("state");

        entity.Property(profile =>
                profile.Country)
            .HasColumnName("country");

        entity.Property(profile =>
                profile.PreferredLanguage)
            .HasColumnName(
                "preferred_language");

        entity.Property(profile =>
                profile.BusinessName)
            .HasColumnName(
                "business_name");

        entity.Property(profile =>
                profile.EntrepreneurialGoal)
            .HasColumnName(
                "entrepreneurial_goal");

        entity.Property(profile =>
                profile.OnboardingStatus)
            .HasColumnName(
                "onboarding_status");

        entity.Property(profile =>
                profile.TermsAcceptedAt)
            .HasColumnName(
                "terms_accepted_at");

        entity.Property(profile =>
                profile.RewardsPolicyAcceptedAt)
            .HasColumnName(
                "rewards_policy_accepted_at");

        entity.Property(profile =>
                profile.CreatedAt)
            .HasColumnName("created_at");

        entity.Property(profile =>
                profile.UpdatedAt)
            .HasColumnName("updated_at");

        entity.HasIndex(profile =>
                profile.UserId)
            .IsUnique();
    });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("password_reset_tokens");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.UserId)
                .HasColumnName("user_id");

            entity.Property(e => e.TokenHash)
                .HasColumnName("token_hash");

            entity.Property(e => e.ExpiresAt)
                .HasColumnName("expires_at");

            entity.Property(e => e.UsedAt)
                .HasColumnName("used_at");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            entity.HasIndex(e => e.TokenHash)
                .IsUnique();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.ToTable("drivers");

            entity.HasKey(driver => driver.Id);

            entity.Property(driver => driver.Id)
                .HasColumnName("Id");

            entity.Property(driver => driver.UserId)
                .HasColumnName("user_id");

            entity.Property(driver => driver.FullName)
                .HasColumnName("FullName");

            entity.Property(driver => driver.PhoneNumber)
                .HasColumnName("PhoneNumber");

            entity.Property(driver => driver.VehicleType)
                .HasColumnName("VehicleType");

            entity.Property(driver => driver.PlateNumber)
                .HasColumnName("PlateNumber");

            entity.Property(driver => driver.AvailabilityStatus)
                .HasColumnName("AvailabilityStatus");

            entity.Property(driver => driver.CreatedAt)
                .HasColumnName("CreatedAt");

            entity.Property(driver => driver.Territory)
                .HasColumnName("Territory");

            entity.Property(driver => driver.ActiveJobs)
                .HasColumnName("ActiveJobs");

            entity.Property(driver => driver.ResponseRate)
                .HasColumnName("ResponseRate");

            entity.Property(driver => driver.LastSeenAt)
                .HasColumnName("LastSeenAt");

            entity.Property(driver => driver.Email)
                .HasColumnName("email");

            entity.Property(driver => driver.PasswordHash)
                .HasColumnName("password_hash");

            entity.HasOne(driver => driver.User)
                .WithMany()
                .HasForeignKey(driver => driver.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TaxCalculation>()
    .HasIndex(x => new { x.OrderId, x.Component, x.TaxType })
    .IsUnique();

        modelBuilder.Entity<TaxLedgerEntry>()
            .Property(x => x.EntryType)
            .HasDefaultValue("calculation");

        modelBuilder.Entity<TaxRule>(entity =>
        {
            entity.ToTable("tax_rules");
            entity.HasKey(e => e.Id);
        });
        // ==========================
        // ORDERS
        // ==========================

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderNumber).HasColumnName("order_number");
            entity.Property(e => e.CustomerName).HasColumnName("customer_name");
            entity.Property(e => e.PickupAddress).HasColumnName("pickup_address");
            entity.Property(e => e.DeliveryAddress).HasColumnName("delivery_address");
            entity.Property(e => e.ItemDescription).HasColumnName("item_description");
            entity.Property(e => e.Zone).HasColumnName("zone");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.DriverId).HasColumnName("driver_id");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(o => o.Supplier)
                .WithMany()
                .HasForeignKey(o => o.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.Driver)
                .WithMany()
                .HasForeignKey(o => o.DriverId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(x => x.CustomerId)
        .HasColumnName("customer_id");

            entity.HasOne(x => x.CustomerUser)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ==========================
        // SUPPLIERS
        // ==========================

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("suppliers");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Name).HasColumnName("Name");
            entity.Property(e => e.ContactNumber).HasColumnName("ContactNumber");
            entity.Property(e => e.Address).HasColumnName("Address");
            entity.Property(e => e.AvailabilityStatus).HasColumnName("AvailabilityStatus");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.Territory).HasColumnName("Territory");
            entity.Property(e => e.CurrentWorkload).HasColumnName("CurrentWorkload");
            entity.Property(e => e.ResponseRate).HasColumnName("ResponseRate");
            entity.Property(e => e.UserId).HasColumnName("user_id");
        });

        // ==========================
        // STATUS HISTORY
        // ==========================

        modelBuilder.Entity<StatusHistory>(entity =>
        {
            entity.ToTable("status_history");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        // ==========================
        // AUDIT LOGS
        // ==========================

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.Action).HasColumnName("action");
            entity.Property(e => e.PerformedBy).HasColumnName("performed_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        // ==========================
        // DELIVERY PROOF
        // ==========================

        modelBuilder.Entity<DeliveryProof>(entity =>
        {
            entity.ToTable("delivery_proof");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ImageUrl).HasColumnName("image_url");
            entity.Property(e => e.UploadedAt).HasColumnName("uploaded_at");
        });

        // ==========================
        // OPERATIONAL ALERTS
        // ==========================

        modelBuilder.Entity<OperationalAlert>(entity =>
        {
            entity.ToTable("operational_alerts");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.AlertType).HasColumnName("alert_type");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.Resolved).HasColumnName("resolved");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        // ==========================
        // PRODUCTS
        // ==========================

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.PartNumber).HasColumnName("part_number");
            entity.Property(e => e.Brand).HasColumnName("brand");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ImageUrl).HasColumnName("image_url");
            entity.Property(e => e.Price).HasColumnName("price");
            entity.Property(e => e.QuantityAvailable).HasColumnName("quantity_available");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        // ==========================
        // CUSTOMERS
        // ==========================

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.FullName).HasColumnName("full_name");
            entity.Property(e => e.Email).HasColumnName("email");
        });

        // ==========================
        // CUSTOMER ADDRESSES
        // ==========================

        modelBuilder.Entity<CustomerAddress>(entity =>
        {
            entity.ToTable("customer_addresses");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.AddressLine1).HasColumnName("address");
            entity.Property(e => e.City).HasColumnName("city");
            entity.Property(e => e.State).HasColumnName("state");
            entity.Property(e => e.ZipCode).HasColumnName("zip_code");

            entity.Ignore(e => e.AddressLine2);
            entity.Ignore(e => e.IsDefault);
            entity.Ignore(e => e.CreatedAt);
        });

        // ==========================
        // CUSTOMER VEHICLES
        // ==========================

        modelBuilder.Entity<CustomerVehicle>(entity =>
        {
            entity.ToTable("customer_vehicles");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.Year).HasColumnName("year");
            entity.Property(e => e.Make).HasColumnName("make");
            entity.Property(e => e.Model).HasColumnName("model");
            entity.Property(e => e.Engine).HasColumnName("engine");
            entity.Property(e => e.Nickname).HasColumnName("nickname");

            entity.Ignore(e => e.IsPrimary);
            entity.Ignore(e => e.CreatedAt);
        });

        // ==========================
        // CART ITEMS
        // ==========================

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.ToTable("cart_items");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");

            entity.Ignore(e => e.CreatedAt);
        });

        // ==========================
        // ORDER ITEMS
        // ==========================

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("order_items");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.UnitPrice).HasColumnName("unit_price");
        });

        // ==========================
        // MECHANICS
        // ==========================

        modelBuilder.Entity<Mechanic>(entity =>
        {
            entity.ToTable("mechanics");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.FullName).HasColumnName("full_name");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.Phone).HasColumnName("phone");
            entity.Property(e => e.ServiceArea).HasColumnName("service_area");
            entity.Property(e => e.AvailabilityStatus).HasColumnName("availability_status");
            entity.Property(e => e.Latitude).HasColumnName("latitude");
            entity.Property(e => e.Longitude).HasColumnName("longitude");
            entity.Property(e => e.ServiceRadiusKm).HasColumnName("service_radius_km");
            entity.Property(e => e.ActiveJobs).HasColumnName("active_jobs");
            entity.Property(e => e.ResponseRate).HasColumnName("response_rate");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        // ==========================
        // SERVICE REQUESTS
        // ==========================

        modelBuilder.Entity<ServiceRequest>(entity =>
        {
            entity.ToTable("service_requests");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.CustomerName).HasColumnName("customer_name");
            entity.Property(e => e.CustomerPhone).HasColumnName("customer_phone");
            entity.Property(e => e.VehicleInfo).HasColumnName("vehicle_info");
            entity.Property(e => e.IssueDescription).HasColumnName("issue_description");
            entity.Property(e => e.ServiceAddress).HasColumnName("service_address");
            entity.Property(e => e.Zone).HasColumnName("zone");
            entity.Property(e => e.Latitude).HasColumnName("latitude");
            entity.Property(e => e.Longitude).HasColumnName("longitude");
            entity.Property(e => e.MechanicId).HasColumnName("mechanic_id");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.PartsRequestNote).HasColumnName("parts_request_note");
            entity.Property(e => e.ProofImageUrl).HasColumnName("proof_image_url");
            entity.Property(e => e.RejectionReason).HasColumnName("rejection_reason");
            entity.Property(e => e.AcceptedAt).HasColumnName("accepted_at");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.ClosedAt).HasColumnName("closed_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.ProviderId).HasColumnName("provider_id");
            entity.Property(e => e.DriverId).HasColumnName("driver_id");
            entity.Property(e => e.PartsRequestId).HasColumnName("parts_request_id");
            entity.Property(e => e.FinalAmount).HasColumnName("final_amount");
            entity.Property(e => e.PaymentStatus).HasColumnName("payment_status");
            entity.Property(e => e.ProviderAcceptedAt).HasColumnName("provider_accepted_at");
            entity.Property(e => e.MechanicAcceptedAt).HasColumnName("mechanic_accepted_at");
            entity.Property(e => e.DriverAssignedAt).HasColumnName("driver_assigned_at");
            entity.Property(e => e.ProofUploadedAt).HasColumnName("proof_uploaded_at");

            entity.HasOne<Supplier>()
                .WithMany()
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<Driver>()
                .WithMany()
                .HasForeignKey(e => e.DriverId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<Mechanic>()
                .WithMany()
                .HasForeignKey(e => e.MechanicId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ==========================
        // PARTS REQUESTS
        // ==========================

        modelBuilder.Entity<PartsRequest>(entity =>
        {
            entity.ToTable("parts_requests");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ServiceRequestId).HasColumnName("service_request_id");
            entity.Property(e => e.MechanicId).HasColumnName("mechanic_id");
            entity.Property(e => e.PartDescription).HasColumnName("part_description");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        // ==========================
        // REPAIR PROOFS
        // ==========================

        modelBuilder.Entity<RepairProof>(entity =>
        {
            entity.ToTable("repair_proofs");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ServiceRequestId).HasColumnName("service_request_id");
            entity.Property(e => e.MechanicId).HasColumnName("mechanic_id");
            entity.Property(e => e.ImageUrl).HasColumnName("image_url");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.UploadedAt).HasColumnName("uploaded_at");
        });

        // ==========================
        // ORDER FINANCIALS
        // ==========================

        modelBuilder.Entity<OrderFinancial>(entity =>
        {
            entity.ToTable("order_financials");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ServiceRequestId).HasColumnName("service_request_id");
            entity.Property(e => e.CustomerPaid).HasColumnName("customer_paid");
            entity.Property(e => e.SupplierAmount).HasColumnName("supplier_amount");
            entity.Property(e => e.DriverAmount).HasColumnName("driver_amount");
            entity.Property(e => e.MechanicAmount).HasColumnName("mechanic_amount");
            entity.Property(e => e.AlphaPlatformFee).HasColumnName("alpha_platform_fee");
            entity.Property(e => e.FinancialStatus).HasColumnName("financial_status");
            entity.Property(e => e.PayoutStatus).HasColumnName("payout_status");
            entity.Property(e => e.CompletionProofUrl).HasColumnName("completion_proof_url");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        // ==========================
        // PAYMENTS
        // ==========================

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.Currency).HasColumnName("currency");
            entity.Property(e => e.PaymentMethod).HasColumnName("payment_method");
            entity.Property(e => e.PaymentStatus).HasColumnName("payment_status");
            entity.Property(e => e.TransactionReference).HasColumnName("transaction_reference");
            entity.Property(e => e.PaidAt).HasColumnName("paid_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        // ==========================
        // SECURITY LOGS
        // ==========================

        modelBuilder.Entity<SecurityLog>(entity =>
        {
            entity.ToTable("security_logs");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.Role).HasColumnName("role");
            entity.Property(e => e.Path).HasColumnName("path");
            entity.Property(e => e.Method).HasColumnName("method");
            entity.Property(e => e.StatusCode).HasColumnName("status_code");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address");
            entity.Property(e => e.UserAgent).HasColumnName("user_agent");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("invoices");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.InvoiceNumber).HasColumnName("invoice_number");
            entity.Property(e => e.Subtotal).HasColumnName("subtotal");
            entity.Property(e => e.Tax).HasColumnName("tax");
            entity.Property(e => e.Total).HasColumnName("total");
            entity.Property(e => e.Currency).HasColumnName("currency");
            entity.Property(e => e.IssuedAt).HasColumnName("issued_at");
        });

       
    }
}