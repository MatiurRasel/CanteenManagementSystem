using CanteenManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CanteenManagementSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Existing tables from ccpc_c217 database
        public DbSet<UTClientSettings> UTClientSettings { get; set; }
        public DbSet<StudentInfo_Canteen> StudentInfo_Canteens { get; set; }
        public DbSet<EmployeeInfo_Canteen> EmployeeInfo_Canteens { get; set; }

        // New tables
        public DbSet<FoodItem> FoodItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<UserBalance> UserBalances { get; set; }
        public DbSet<DailyMenu> DailyMenus { get; set; }
        public DbSet<WeeklyMenuTemplate> WeeklyMenuTemplates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UTClientSettings>()
                .HasQueryFilter(w => w.SrlNo == 33);

            // Configure existing tables
            modelBuilder.Entity<StudentInfo_Canteen>()
                .HasKey(s => s.StudentID);

            modelBuilder.Entity<EmployeeInfo_Canteen>()
                .HasKey(e => e.EmployeeID);

            // Configure UserBalance - Composite key for UserId + UserType
            modelBuilder.Entity<UserBalance>()
                .HasKey(ub => ub.BalanceID);

            modelBuilder.Entity<UserBalance>()
                .HasIndex(ub => new { ub.UserId, ub.UserType })
                .IsUnique();

            // Configure Order relationships
            modelBuilder.Entity<Order>()
                .HasOne(o => o.UserBalance)
                .WithMany(ub => ub.Orders)
                .HasForeignKey(o => new { o.UserId, o.UserType })
                .HasPrincipalKey(ub => new { ub.UserId, ub.UserType })
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.FoodItem)
                .WithMany()
                .HasForeignKey(oi => oi.FoodItemID)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure DailyMenu relationships
            modelBuilder.Entity<DailyMenu>()
                .HasOne(dm => dm.FoodItem)
                .WithMany()
                .HasForeignKey(dm => dm.FoodItemID)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure WeeklyMenuTemplate relationships
            modelBuilder.Entity<WeeklyMenuTemplate>()
                .HasOne(wmt => wmt.FoodItem)
                .WithMany()
                .HasForeignKey(wmt => wmt.FoodItemID)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes for performance
            modelBuilder.Entity<Order>()
                .HasIndex(o => o.OrderNumber)
                .IsUnique();

            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.OrderDate, o.Status });

            modelBuilder.Entity<DailyMenu>()
                .HasIndex(dm => dm.MenuDate);

            // Configure decimal precision
            modelBuilder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasPrecision(10, 2);

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.UnitPrice)
                .HasPrecision(10, 2);

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.TotalPrice)
                .HasPrecision(10, 2);

            modelBuilder.Entity<UserBalance>()
                .Property(ub => ub.TotalBalance)
                .HasPrecision(10, 2);

            modelBuilder.Entity<UserBalance>()
                .Property(ub => ub.UsedBalance)
                .HasPrecision(10, 2);

            modelBuilder.Entity<FoodItem>()
                .Property(f => f.Price)
                .HasPrecision(10, 2);
        }
    }
}
