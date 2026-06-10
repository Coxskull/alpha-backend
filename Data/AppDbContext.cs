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

    // MVP Lifecycle Tables
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<Driver> Drivers { get; set; }
    public DbSet<StatusHistory> StatusHistory { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<DeliveryProof> DeliveryProofs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .ToTable("users");

        modelBuilder.Entity<Order>()
            .ToTable("orders");

        modelBuilder.Entity<Driver>()
            .ToTable("drivers");

        modelBuilder.Entity<Supplier>()
            .ToTable("suppliers");

        modelBuilder.Entity<AuditLog>()
            .ToTable("audit_logs");

        modelBuilder.Entity<StatusHistory>()
            .ToTable("status_history");

        modelBuilder.Entity<DeliveryProof>()
            .ToTable("delivery_proof");
        modelBuilder.Entity<Order>()
    .HasOne(o => o.Supplier)
    .WithMany()
    .HasForeignKey(o => o.SupplierId);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Driver)
            .WithMany()
            .HasForeignKey(o => o.DriverId);
    }
}