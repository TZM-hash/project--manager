using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectAssignment> ProjectAssignments => Set<ProjectAssignment>();

    public DbSet<ProjectStatus> ProjectStatuses => Set<ProjectStatus>();

    public DbSet<ProjectStatusStyle> ProjectStatusStyles => Set<ProjectStatusStyle>();

    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();

    public DbSet<MonthlySettlementBatch> MonthlySettlementBatches => Set<MonthlySettlementBatch>();

    public DbSet<MonthlySettlementItem> MonthlySettlementItems => Set<MonthlySettlementItem>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Project>(entity =>
        {
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
            entity.HasIndex(x => new { x.Year, x.Month, x.BatchNumber }).IsUnique();

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MonthlySettlementItem>(entity =>
        {
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
            entity.HasIndex(x => x.ProjectId);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
