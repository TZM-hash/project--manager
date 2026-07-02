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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Project>(entity =>
        {
            // 项目工号只要求“未删除项目”内唯一，软删除后允许重新使用同年度工号。
            entity.HasIndex(x => new { x.Year, x.ProjectNumber })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

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

            // 更新人是审计上下文，不允许删除用户时级联删除项目。
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

            entity.HasOne(x => x.Project)
                .WithMany(x => x.PurchaseRequests)
                .HasForeignKey(x => x.ProjectId);

            // 人员类外键统一 Restrict，避免删除账号时影响历史项目和请购数据。
            entity.HasOne(x => x.PurchaseStaff)
                .WithMany()
                .HasForeignKey(x => x.PurchaseStaffUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SubCaseContact)
                .WithMany()
                .HasForeignKey(x => x.SubCaseContactUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MonthlySettlementBatch>(entity =>
        {
            // 同一个年月可以生成多次月结，每次生成按 BatchNumber 递增。
            entity.HasIndex(x => new { x.Year, x.Month, x.BatchNumber }).IsUnique();

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MonthlySettlementItem>(entity =>
        {
            // 月结明细保存生成当时的项目快照，因此保留较多文本汇总字段。
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
            // 项目详情页按 ProjectId 拉取操作记录，该索引避免历史日志增长后查询变慢。
            entity.HasIndex(x => x.ProjectId);

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
            entity.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.HandoverPercent).HasPrecision(5, 2);

            entity.HasOne(x => x.Executor)
                .WithMany()
                .HasForeignKey(x => x.ExecutorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.UpdatedByUser)
                .WithMany()
                .HasForeignKey(x => x.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
