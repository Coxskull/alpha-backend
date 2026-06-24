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

    public DbSet<Mechanic> Mechanics { get; set; }
    public DbSet<ServiceRequest> ServiceRequests { get; set; }
    public DbSet<OrderFinancial> OrderFinancials { get; set; }
    public DbSet<Payment> Payments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


        // ==========================
        // TABLE MAPPINGS
        // ==========================

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("Id");

            entity.Property(e => e.FullName)
                .HasColumnName("FullName");

            entity.Property(e => e.Email)
                .HasColumnName("Email");

            entity.Property(e => e.Phone)
                .HasColumnName("Phone");

            entity.Property(e => e.Role)
                .HasColumnName("Role");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("CreatedAt");

            entity.Property(e => e.PasswordHash)
                .HasColumnName("PasswordHash");
        });
        modelBuilder.Entity<Order>().ToTable("orders");
        modelBuilder.Entity<Supplier>().ToTable("suppliers");
        modelBuilder.Entity<Driver>().ToTable("drivers");
        modelBuilder.Entity<StatusHistory>().ToTable("status_history");
        modelBuilder.Entity<AuditLog>().ToTable("audit_logs");
        modelBuilder.Entity<DeliveryProof>().ToTable("delivery_proof");
        modelBuilder.Entity<OperationalAlert>().ToTable("operational_alerts");

        modelBuilder.Entity<Product>().ToTable("products");
        modelBuilder.Entity<Customer>().ToTable("customers");
        modelBuilder.Entity<CustomerAddress>().ToTable("customer_addresses");
        modelBuilder.Entity<CustomerVehicle>().ToTable("customer_vehicles");
        modelBuilder.Entity<CartItem>().ToTable("cart_items");

        // ==========================
        // SUPPLIER MAPPINGS
        // ==========================

        modelBuilder.Entity<Supplier>()
     .Property(x => x.Territory)
     .HasColumnName("Territory");

        modelBuilder.Entity<Supplier>()
            .Property(x => x.CurrentWorkload)
            .HasColumnName("CurrentWorkload");

        modelBuilder.Entity<Supplier>()
            .Property(x => x.ResponseRate)
            .HasColumnName("ResponseRate");

        // ==========================
        // DRIVER MAPPINGS
        // ==========================

        modelBuilder.Entity<Driver>()
     .Property(x => x.Territory)
     .HasColumnName("Territory");

        modelBuilder.Entity<Driver>()
            .Property(x => x.ActiveJobs)
            .HasColumnName("ActiveJobs");

        modelBuilder.Entity<Driver>()
            .Property(x => x.ResponseRate)
            .HasColumnName("ResponseRate");

        modelBuilder.Entity<Driver>()
            .Property(x => x.LastSeenAt)
            .HasColumnName("LastSeenAt");

        // ==========================
        // PRODUCT MAPPINGS
        // ==========================

        modelBuilder.Entity<Product>()
            .ToTable("products");

        // ==========================
        // CUSTOMER ADDRESS MAPPINGS
        // ==========================

        modelBuilder.Entity<CustomerAddress>()
            .Property(x => x.CustomerId)
            .HasColumnName("customer_id");

        modelBuilder.Entity<CustomerAddress>()
            .Property(x => x.AddressLine1)
            .HasColumnName("address_line1");

        modelBuilder.Entity<CustomerAddress>()
            .Property(x => x.AddressLine2)
            .HasColumnName("address_line2");

        modelBuilder.Entity<CustomerAddress>()
            .Property(x => x.ZipCode)
            .HasColumnName("zip_code");

        modelBuilder.Entity<CustomerAddress>()
            .Property(x => x.IsDefault)
            .HasColumnName("is_default");

        modelBuilder.Entity<CustomerAddress>()
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at");

        // ==========================
        // CUSTOMER VEHICLE MAPPINGS
        // ==========================

        modelBuilder.Entity<CustomerVehicle>()
            .Property(x => x.CustomerId)
            .HasColumnName("customer_id");

        modelBuilder.Entity<CustomerVehicle>()
            .Property(x => x.IsPrimary)
            .HasColumnName("is_primary");

        modelBuilder.Entity<CustomerVehicle>()
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at");

        // ==========================
        // CART ITEM MAPPINGS
        // ==========================

        modelBuilder.Entity<CartItem>()
            .Property(x => x.CustomerId)
            .HasColumnName("customer_id");

        modelBuilder.Entity<CartItem>()
            .Property(x => x.ProductId)
            .HasColumnName("product_id");

        modelBuilder.Entity<CartItem>()
            .Property(x => x.CreatedAt)
            .HasColumnName("created_at");

        // ==========================
        // ORDER RELATIONSHIPS
        // ==========================

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Supplier)
            .WithMany()
            .HasForeignKey(o => o.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Driver)
            .WithMany()
            .HasForeignKey(o => o.DriverId)
            .OnDelete(DeleteBehavior.Restrict);


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

        modelBuilder.Entity<ServiceRequest>(entity =>
        {
            entity.ToTable("service_requests");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
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
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<OrderFinancial>()
    .ToTable("order_financials");

        modelBuilder.Entity<Payment>()
            .ToTable("payments");
    }
}