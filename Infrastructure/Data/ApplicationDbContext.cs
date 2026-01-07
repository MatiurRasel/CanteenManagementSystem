using CanteenManagementSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CanteenManagementSystem.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Multi-tenant tables
        public DbSet<Client> Clients { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserBalance> UsersWallet { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<NfcCard> NfcCards { get; set; }

        // Menu tables
        public DbSet<MenuCategory> MenuCategories { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<MenuItemVariant> MenuItemVariants { get; set; }

        // Order tables
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        // Inventory tables
        public DbSet<Inventory> Inventory { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<WastageLog> WastageLog { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Client configuration
            modelBuilder.Entity<Client>()
                .HasIndex(c => c.Subdomain)
                .IsUnique();

            // User configuration
            modelBuilder.Entity<User>()
                .HasIndex(u => new { u.ClientId, u.Email })
                .IsUnique()
                .HasFilter("[Email] IS NOT NULL");

            modelBuilder.Entity<User>()
                .HasIndex(u => u.RollNumber)
                .HasFilter("[RollNumber] IS NOT NULL");

            // Wallet configuration
            modelBuilder.Entity<UserBalance>()
                .HasIndex(ub => ub.UserId)
                .IsUnique();

            // NFC Card configuration
            modelBuilder.Entity<NfcCard>()
                .HasIndex(c => c.CardNumber)
                .IsUnique();

            modelBuilder.Entity<NfcCard>()
                .HasIndex(c => new { c.ClientId, c.CardNumber });

            // Order configuration
            modelBuilder.Entity<Order>()
                .HasIndex(o => o.OrderNumber)
                .IsUnique();

            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.OrderStatus, o.CreatedAt });

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.AutoCancelAt)
                .HasFilter("[AutoCancelAt] IS NOT NULL AND [OrderStatus] = 'READY'");

            // Order Items
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Inventory configuration
            modelBuilder.Entity<Inventory>()
                .HasIndex(i => new { i.ClientId, i.ItemId })
                .IsUnique();

            // Decimal precision
            modelBuilder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasPrecision(10, 2);

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.UnitPrice)
                .HasPrecision(10, 2);

            modelBuilder.Entity<UserBalance>()
                .Property(ub => ub.MainBalance)
                .HasPrecision(10, 2);

            modelBuilder.Entity<MenuItem>()
                .Property(m => m.BasePrice)
                .HasPrecision(10, 2);
        }
    }
}

