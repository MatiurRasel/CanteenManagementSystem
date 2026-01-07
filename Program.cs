using CanteenManagementSystem.Application.Interfaces;
using CanteenManagementSystem.Application.Services;
using CanteenManagementSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddControllers(); // For API endpoints

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register Application Services (SOLID - Dependency Injection)
// Note: Order matters due to circular dependencies - register in dependency order
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<INfcService, NfcService>();

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add CORS for API access
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseCors("AllowAll");

app.UseAuthorization();
app.UseSession();

// Map API routes
app.MapControllers();

// Map MVC routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Verification}/{action=Index}/{id?}");

app.Run();
