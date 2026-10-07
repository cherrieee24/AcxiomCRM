using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AcxiomCRM.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private static readonly HashSet<string> TrackingColumns = new()
    {
        nameof(AuditableEntity.CreatedDate), nameof(AuditableEntity.CreatedBy),
        nameof(AuditableEntity.ModifiedDate), nameof(AuditableEntity.ModifiedBy),
        nameof(AuditableEntity.IsDeleted)
    };

    private readonly IHttpContextAccessor? _httpContextAccessor;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor? httpContextAccessor = null)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Store enums as readable strings and decimals as REAL so SQLite can SUM/ORDER BY them.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<decimal>().HaveConversion<double>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            e.HasOne(u => u.Manager).WithMany().HasForeignKey(u => u.ManagerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Customer>(e =>
        {
            e.HasQueryFilter(c => !c.IsDeleted);
            e.HasIndex(c => c.CustomerCode).IsUnique();
            e.HasIndex(c => c.Email).IsUnique().HasFilter("\"IsDeleted\" = 0");
            e.HasIndex(c => c.Phone).IsUnique().HasFilter("\"IsDeleted\" = 0");
            e.HasIndex(c => c.AssignedTo);
            e.HasOne(c => c.AssignedUser).WithMany().HasForeignKey(c => c.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Lead>(e =>
        {
            e.HasQueryFilter(l => !l.IsDeleted);
            e.HasIndex(l => l.LeadCode).IsUnique();
            e.HasIndex(l => l.Status);
            e.HasIndex(l => l.AssignedTo);
            e.HasOne(l => l.AssignedUser).WithMany().HasForeignKey(l => l.AssignedTo).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.ConvertedCustomer).WithMany().HasForeignKey(l => l.ConvertedCustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Opportunity>(e =>
        {
            e.HasQueryFilter(o => !o.IsDeleted);
            e.HasIndex(o => o.Stage);
            e.HasIndex(o => o.AssignedTo);
            e.Ignore(o => o.WeightedAmount);
            e.HasOne(o => o.Customer).WithMany(c => c.Opportunities).HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.Lead).WithMany().HasForeignKey(o => o.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.AssignedUser).WithMany().HasForeignKey(o => o.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FollowUp>(e =>
        {
            e.HasQueryFilter(f => !f.IsDeleted);
            e.HasIndex(f => new { f.Status, f.FollowUpDate });
            e.HasIndex(f => f.AssignedTo);
            e.Ignore(f => f.IsOverdue);
            e.HasOne(f => f.Customer).WithMany(c => c.FollowUps).HasForeignKey(f => f.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Lead).WithMany(l => l.FollowUps).HasForeignKey(f => f.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Opportunity).WithMany(o => o.FollowUps).HasForeignKey(f => f.OpportunityId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.AssignedUser).WithMany().HasForeignKey(f => f.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Activity>(e =>
        {
            e.HasQueryFilter(a => !a.IsDeleted);
            e.HasIndex(a => a.AssignedTo);
            e.HasOne(a => a.Customer).WithMany(c => c.Activities).HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Lead).WithMany(l => l.Activities).HasForeignKey(a => a.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.AssignedUser).WithMany().HasForeignKey(a => a.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => a.CreatedDate);
            e.HasIndex(a => new { a.EntityName, a.Action });
            e.HasIndex(a => a.UserId);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var (userId, userName, ip) = CurrentUser();
        var now = DateTime.UtcNow;
        var pending = new List<(EntityEntry Entry, AuditLog Log)>();

        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditLog)
            {
                if (entry.State is EntityState.Modified or EntityState.Deleted)
                    throw new InvalidOperationException("Audit log records are append-only and cannot be modified or deleted.");
                continue;
            }

            if (entry.Entity is not AuditableEntity auditable) continue;

            var entityName = entry.Metadata.ClrType.Name;
            AuditLog? log = null;
            switch (entry.State)
            {
                case EntityState.Added:
                    auditable.CreatedDate = now;
                    auditable.CreatedBy ??= userId;
                    log = NewLog(entityName, AuditActions.Create, null, Snapshot(entry, useOriginal: false, onlyModified: false));
                    break;

                case EntityState.Modified:
                    var wasDeleted = (bool)entry.Property(nameof(AuditableEntity.IsDeleted)).OriginalValue!;
                    var softDeleted = auditable.IsDeleted && !wasDeleted;
                    if (!softDeleted && !entry.Properties.Any(p => p.IsModified && !TrackingColumns.Contains(p.Metadata.Name)))
                        break; // nothing meaningful changed

                    auditable.ModifiedDate = now;
                    auditable.ModifiedBy = userId;
                    log = softDeleted
                        ? NewLog(entityName, AuditActions.Delete, Snapshot(entry, useOriginal: true, onlyModified: false), null)
                        : NewLog(entityName, AuditActions.Update, Snapshot(entry, useOriginal: true, onlyModified: true), Snapshot(entry, useOriginal: false, onlyModified: true));
                    break;

                case EntityState.Deleted:
                    log = NewLog(entityName, AuditActions.Delete, Snapshot(entry, useOriginal: true, onlyModified: false), null);
                    break;
            }

            if (log != null) pending.Add((entry, log));
        }

        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        if (pending.Count > 0)
        {
            foreach (var (entry, log) in pending)
            {
                // Primary keys are only known after the first save for new rows.
                var key = entry.Metadata.FindPrimaryKey()!.Properties.Select(p => entry.Property(p.Name).CurrentValue);
                log.RecordId = string.Join(",", key);
                AuditLogs.Add(log);
            }
            await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        return result;

        AuditLog NewLog(string entityName, string action, string? oldValue, string? newValue) => new()
        {
            UserId = userId,
            UserName = userName,
            Action = action,
            EntityName = entityName,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedDate = now,
            IpAddress = ip,
            Result = "Success"
        };
    }

    // Enums are stored by name so audit history stays readable ("Active", not 0).
    private static readonly JsonSerializerOptions SnapshotJson = new() { Converters = { new JsonStringEnumConverter() } };

    private static string Snapshot(EntityEntry entry, bool useOriginal, bool onlyModified)
    {
        var values = entry.Properties
            .Where(p => !TrackingColumns.Contains(p.Metadata.Name))
            .Where(p => !onlyModified || p.IsModified)
            .ToDictionary(p => p.Metadata.Name, p => useOriginal ? p.OriginalValue : p.CurrentValue);
        return JsonSerializer.Serialize(values, SnapshotJson);
    }

    private (string? UserId, string? UserName, string? Ip) CurrentUser()
    {
        var ctx = _httpContextAccessor?.HttpContext;
        var user = ctx?.User;
        return (
            user?.FindFirstValue(ClaimTypes.NameIdentifier),
            user?.Identity?.Name,
            ctx?.Connection.RemoteIpAddress?.ToString());
    }
}
