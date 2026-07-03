using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    // 让“保持登录”真正跨浏览器重开生效，并在活跃使用时自动续期。
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

builder.Services.AddRazorPages();
builder.Services.AddScoped<ProjectQueryService>();
builder.Services.AddScoped<ProjectMaintenanceService>();
builder.Services.AddScoped<StatusMaintenanceService>();
builder.Services.AddScoped<WorkbenchProjectService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<SettlementService>();
builder.Services.AddScoped<ExcelReportService>();
builder.Services.AddScoped<DataExchangeService>();
builder.Services.AddScoped<PlanningProjectService>();
builder.Services.AddScoped<MaintenanceOrderService>();
builder.Services.AddScoped<ProjectGanttService>();
builder.Services.AddScoped<UserLookupService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

await SeedData.EnsureSeededAsync(app.Services);

app.Run();

public partial class Program
{
}
