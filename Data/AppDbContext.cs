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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ==========================
        // TABLE MAPPINGS
        // ==========================

        modelBuilder.Entity<User>().ToTable("users");
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
            .HasColumnName("territory");

        modelBuilder.Entity<Supplier>()
            .Property(x => x.CurrentWorkload)
            .HasColumnName("current_workload");

        // ==========================
        // DRIVER MAPPINGS
        // ==========================

        modelBuilder.Entity<Driver>()
            .Property(x => x.Territory)
            .HasColumnName("territory");

        modelBuilder.Entity<Driver>()
            .Property(x => x.ActiveJobs)
            .HasColumnName("active_jobs");

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
    }
}