using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    // 业务实体 DbSet 集中放在这里，Identity 相关表由 IdentityDbContext 自动提供。
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectAssignment> ProjectAssignments => Set<ProjectAssignment>();

    public DbSet<ProjectStatus> ProjectStatuses => Set<ProjectStatus>();

    public DbSet<ProjectStatusStyle> ProjectStatusStyles => Set<ProjectStatusStyle>();

    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();

    public DbSet<MonthlySettlementBatch> MonthlySettlementBatches => Set<MonthlySettlementBatch>();

    public DbSet<MonthlySettlementItem> MonthlySettlementItems => Set<MonthlySettlementItem>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<PlanningProject> PlanningProjects => Set<PlanningProject>();

    public DbSet<PlanningProjectHistoryRecord> PlanningProjectHistoryRecords => Set<PlanningProjectHistoryRecord>();

    public DbSet<MaintenanceOrder> MaintenanceOrders => Set<MaintenanceOrder>();

    public DbSet<ProjectGanttPlan> ProjectGanttPlans => Set<ProjectGanttPlan>();

    public DbSet<ProjectGanttTask> ProjectGanttTasks => Set<ProjectGanttTask>();

    public DbSet<ProjectSkippedStatus> ProjectSkippedStatuses => Set<ProjectSkippedStatus>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public DbSet<Vendor> Vendors => Set<Vendor>();

    public DbSet<VendorContact> VendorContacts => Set<VendorContact>();

    public DbSet<ProjectArchive> ProjectArchives => Set<ProjectArchive>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Project>(entity =>
        {
            // 專案工號只要求「未刪除專案」內唯一，軟刪除後允許重新使用同年度工號。
            entity.HasIndex(x => new { x.Year, x.ProjectNumber })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.StatusId);
            entity.HasIndex(x => x.UpdatedAt);
            entity.HasIndex(x => x.ProjectNumber);
            entity.HasIndex(x => x.ParentCaseNumber);
            entity.HasIndex(x => x.ProjectType);

            entity.Property(x => x.ParentCaseNumber).HasMaxLength(64);
            entity.Property(x => x.ProjectNumber).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ProjectAmount).HasPrecision(18, 2);
            entity.Property(x => x.ProgressPercent).HasPrecision(5, 2);
            entity.Property(x => x.CollectionPercent).HasPrecision(5, 2);

            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            // 更新人是審計上下文，不允许刪除使用者时级联刪除專案。
            entity.HasOne(x => x.UpdatedByUser)
                .WithMany()
                .HasForeignKey(x => x.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectAssignment>(entity =>
        {
            entity.Property(x => x.UserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.RoleInProject).HasMaxLength(80).IsRequired();

            entity.HasOne(x => x.Project)
                .WithMany(x => x.Assignments)
                .HasForeignKey(x => x.ProjectId);
            entity.HasIndex(x => new { x.UserId, x.ProjectId });

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectStatus>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
        });

        builder.Entity<ProjectStatusStyle>(entity =>
        {
            entity.HasIndex(x => x.StatusId).IsUnique();
            entity.Property(x => x.TextColor).HasMaxLength(16).IsRequired();
            entity.Property(x => x.BackgroundColor).HasMaxLength(16).IsRequired();

            entity.HasOne(x => x.Status)
                .WithOne(x => x.Style)
                .HasForeignKey<ProjectStatusStyle>(x => x.StatusId);
        });

        builder.Entity<PurchaseRequest>(entity =>
        {
            entity.Property(x => x.RequestNumber).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PurchaseAmount).HasPrecision(18, 2);
            entity.Property(x => x.PaymentPercent).HasPrecision(5, 2);
            entity.Property(x => x.ActualPaidAmount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.ProjectId, x.IsDeleted });

            entity.HasOne(x => x.Project)
                .WithMany(x => x.PurchaseRequests)
                .HasForeignKey(x => x.ProjectId);

            // 人员类外键统一 Restrict，避免刪除帳號时影响歷史專案和请购資料。
            entity.HasOne(x => x.PurchaseStaff)
                .WithMany()
                .HasForeignKey(x => x.PurchaseStaffUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SubCaseContact)
                .WithMany()
                .HasForeignKey(x => x.SubCaseContactUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.VendorContact)
                .WithMany()
                .HasForeignKey(x => x.VendorContactId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MonthlySettlementBatch>(entity =>
        {
            // 同一个年月可以生成多次月結，每次生成按 BatchNumber 递增。
            entity.HasIndex(x => new { x.Year, x.Month, x.BatchNumber }).IsUnique();

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MonthlySettlementItem>(entity =>
        {
            // 月結明細儲存生成当时的專案快照，因此保留较多文本彙總欄位。
            entity.Property(x => x.ParentCaseNumber).HasMaxLength(64);
            entity.Property(x => x.ProjectNumber).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ProjectName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ProjectAmount).HasPrecision(18, 2);
            entity.Property(x => x.ProgressPercent).HasPrecision(5, 2);
            entity.Property(x => x.CollectionPercent).HasPrecision(5, 2);
            entity.Property(x => x.PurchaseAmountTotal).HasPrecision(18, 2);
            entity.Property(x => x.ActualPaidAmountTotal).HasPrecision(18, 2);

            entity.HasOne(x => x.Batch)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.BatchId);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.Property(x => x.Action).HasMaxLength(80).IsRequired();
            entity.Property(x => x.EntityName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(120).IsRequired();
            entity.Property(x => x.ProjectNumber).HasMaxLength(64);
            entity.Property(x => x.ChangeSummary).HasMaxLength(500);
            // 專案明細頁按 ProjectId 拉取操作記錄，该索引避免歷史日誌增长后查詢变慢。
            entity.HasIndex(x => x.ProjectId);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.ProjectId, x.CreatedAt });
            entity.HasIndex(x => new { x.EntityName, x.EntityId });

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PlanningProject>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Vendor).HasMaxLength(200);

            entity.HasOne(x => x.Leader)
                .WithMany()
                .HasForeignKey(x => x.LeaderUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PlanningProjectHistoryRecord>(entity =>
        {
            entity.HasIndex(x => new { x.PlanningProjectId, x.Year, x.Month });

            entity.HasOne(x => x.PlanningProject)
                .WithMany(x => x.HistoryRecords)
                .HasForeignKey(x => x.PlanningProjectId);

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MaintenanceOrder>(entity =>
        {
            entity.Property(x => x.ContractNumber).HasMaxLength(50).IsRequired();
            entity.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.SiteName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.OnSiteSoftwareFrequency).HasMaxLength(50).IsRequired();
            entity.Property(x => x.OnSiteHardwareFrequency).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ProgressPercent).HasPrecision(5, 2);
            entity.Property(x => x.HandoverPercent).HasPrecision(5, 2);
            entity.Property(x => x.MaintenanceDescription).HasMaxLength(1000).IsRequired();

            entity.HasOne(x => x.Executor)
                .WithMany()
                .HasForeignKey(x => x.ExecutorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.UpdatedByUser)
                .WithMany()
                .HasForeignKey(x => x.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectGanttPlan>(entity =>
        {
            entity.HasIndex(x => x.ProjectId).IsUnique();
            entity.Property(x => x.ProgressNote).HasMaxLength(2000);

            entity.HasOne(x => x.Project)
                .WithOne(x => x.GanttPlan)
                .HasForeignKey<ProjectGanttPlan>(x => x.ProjectId);

            entity.HasOne(x => x.UpdatedByUser)
                .WithMany()
                .HasForeignKey(x => x.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectGanttTask>(entity =>
        {
            entity.HasIndex(x => new { x.ProjectGanttPlanId, x.SortOrder });
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ProgressPercent).HasPrecision(5, 2);
            entity.Property(x => x.ProgressDescription).HasMaxLength(1000);

            entity.HasOne(x => x.ProjectGanttPlan)
                .WithMany(x => x.Tasks)
                .HasForeignKey(x => x.ProjectGanttPlanId);
        });

        builder.Entity<ProjectSkippedStatus>(entity =>
        {
            entity.HasIndex(x => new { x.ProjectId, x.StatusId }).IsUnique();

            entity.HasOne(x => x.Project)
                .WithMany(x => x.SkippedStatuses)
                .HasForeignKey(x => x.ProjectId);

            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SystemSetting>(entity =>
        {
            entity.HasIndex(x => x.Key).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Value).HasMaxLength(400).IsRequired();
        });

        builder.Entity<Vendor>(entity =>
        {
            entity.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.IsDeleted);
        });

        builder.Entity<VendorContact>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(20);
            entity.HasIndex(x => x.VendorId);
            entity.HasIndex(x => x.IsDeleted);

            entity.HasOne(x => x.Vendor)
                .WithMany(x => x.Contacts)
                .HasForeignKey(x => x.VendorId);
        });

        builder.Entity<ProjectArchive>(entity =>
        {
            entity.HasIndex(x => x.OriginalProjectId);
            entity.HasIndex(x => x.ProjectNumber);
            entity.HasIndex(x => x.ParentCaseNumber);
            entity.HasIndex(x => x.ArchivedAt);
            entity.HasIndex(x => x.Year);

            entity.Property(x => x.ParentCaseNumber).HasMaxLength(64);
            entity.Property(x => x.ProjectNumber).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.StatusName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ProjectAmount).HasPrecision(18, 2);
            entity.Property(x => x.ProgressPercent).HasPrecision(5, 2);
            entity.Property(x => x.CollectionPercent).HasPrecision(5, 2);
            entity.Property(x => x.AssignmentSummary).HasMaxLength(500);

            entity.HasOne(x => x.ArchivedByUser)
                .WithMany()
                .HasForeignKey(x => x.ArchivedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
