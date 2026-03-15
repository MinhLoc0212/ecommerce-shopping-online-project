using Microsoft.EntityFrameworkCore;
using GearsHouse.Models;
using GearsHouse.Repositories;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using GearsHouse.Services;
using GearsHouse.Models;
using GearsHouse.Repositories;




var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<InvoicePdfGenerator>();
builder.Services.Configure<VNPaySettings>(builder.Configuration.GetSection("VNPay"));
builder.Services.AddScoped<VNPayService>();
builder.Services.Configure<MomoSettings>(builder.Configuration.GetSection("MomoAPI"));
builder.Services.AddScoped<MomoService>();


// Đặt trước AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));



builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddRoles<IdentityRole>()
    .AddDefaultTokenProviders()
    .AddDefaultUI()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddRazorPages();

builder.Services.AddScoped<IProductRepository, EFProductRepository>();
builder.Services.AddScoped<ICategoryRepository, EFCategoryRepository>();
builder.Services.AddScoped<IBrandRepository, BrandRepository>();


builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";

    

});




var app = builder.Build();

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseSession();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.MapRazorPages();

//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Home}/{action=Index}/{id?}");

app.UseRouting();
app.UseEndpoints(endpoints =>
{
    // Đảm bảo không dùng Area, và sử dụng controller Home với action Dashboard
    endpoints.MapControllerRoute(
        name: "dashboard",
        pattern: "Home/Dashboard",  // Đường dẫn đến Dashboard của HomeController
        defaults: new { controller = "Home", action = "Dashboard" });

    // Route mặc định
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
});
app.MapRazorPages();
    app.Run();

