using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;
using ProjectManager.Web.Services.DataViews;
using ProjectManager.Web.Pages.Shared;
using Microsoft.AspNetCore.DataProtection;
using ProjectManager.Web.Services.Workbench;
using ProjectManager.Web.Services.Operations;
using ProjectManager.Web.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 1;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    // 让“保持登入”真正跨浏览器重开生效，并在活跃使用时自动续期。
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

builder.Services.AddRazorPages();
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Logging.ClearProviders();
    builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
}
builder.Services.AddScoped<ProjectQueryService>();
builder.Services.AddScoped<ProjectMaintenanceService>();
builder.Services.AddScoped<StatusMaintenanceService>();
builder.Services.AddScoped<WorkbenchProjectService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<AuditTrailQueryService>();
builder.Services.AddScoped<SettlementService>();
builder.Services.AddScoped<ExcelReportService>();
builder.Services.AddScoped<DataExchangeService>();
builder.Services.AddScoped<PlanningProjectService>();
builder.Services.AddScoped<MaintenanceOrderService>();
builder.Services.AddScoped<ProjectGanttService>();
builder.Services.AddScoped<ProjectCollaborationService>();
var appDataRoot = builder.Environment.IsEnvironment("Testing")
    ? Path.Combine(Path.GetTempPath(), "ProjectManager.Tests", "app-data")
    : Path.Combine(builder.Environment.ContentRootPath, "App_Data");
builder.Services.Configure<OperationStorageOptions>(options =>
    options.RootPath = Path.Combine(appDataRoot, "operations"));
builder.Services.Configure<OperationalMonitoringOptions>(options =>
{
    options.LogRootPath = Path.Combine(appDataRoot, "logs");
    options.DataRootPath = appDataRoot;
});
builder.Services.Configure<ProjectCollaborationAttachmentStorageOptions>(options =>
    options.RootPath = Path.Combine(appDataRoot, "collaboration-attachments"));
builder.Services.AddSingleton<ProjectCollaborationAttachmentStore>();
builder.Services.AddSingleton<OperationWorkerHeartbeat>();
builder.Services.AddSingleton<OperationFileStore>();
builder.Services.AddScoped<OperationJobService>();
builder.Services.AddScoped<IOperationJobHandler, FullExportOperationHandler>();
builder.Services.AddScoped<IOperationJobHandler, FullImportOperationHandler>();
builder.Services.AddScoped<IOperationJobHandler, ProjectBulkDeleteOperationHandler>();
builder.Services.AddScoped<IOperationJobHandler, MaintenanceBulkDeleteOperationHandler>();
builder.Services.AddScoped<OperationHandlerDispatcher>();
builder.Services.AddSingleton<ExceptionLogStore>();
builder.Services.AddScoped<OperationalHealthService>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["ready"]);
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<OperationJobWorker>();
}
builder.Services.AddScoped<UserLookupService>();
builder.Services.AddScoped<SystemSettingsService>();
builder.Services.AddScoped<ProjectArchiveService>();
builder.Services.AddSingleton<DataViewRegistry>();
builder.Services.AddScoped<SavedDataViewService>();
builder.Services.AddScoped<SavedDataViewPageSupport>();
builder.Services.AddScoped<PersonalWorkbenchService>();
builder.Services.AddSingleton<OpenCcConverterService>();
builder.Services.AddSingleton<HtmlLanguageConverter>();
builder.Services.AddSingleton(TimeProvider.System);

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

app.UseMiddleware<ExceptionLogMiddleware>();

app.UseHttpsRedirection();
app.UseMiddleware<DisplayLanguageMiddleware>();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});

await SeedData.EnsureSeededAsync(app.Services);

app.Run();

public partial class Program
{
}
