using Master.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Master.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<UserPlan> UserPlans => Set<UserPlan>();
    public DbSet<Server> Servers => Set<Server>();
    public DbSet<Node> Nodes => Set<Node>();
    public DbSet<NodeUserBinding> NodeUserBindings => Set<NodeUserBinding>();
    public DbSet<TrafficRecord> TrafficRecords => Set<TrafficRecord>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 索引
        modelBuilder.Entity<TrafficRecord>()
            .HasIndex(t => new { t.UserId, t.Date });
        modelBuilder.Entity<TrafficRecord>()
            .HasIndex(t => new { t.ServerId, t.Date });
        modelBuilder.Entity<Server>()
            .HasIndex(s => s.AgentTokenHash).IsUnique();
        modelBuilder.Entity<User>()
            .HasIndex(u => u.SubscriptionToken).IsUnique();
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username).IsUnique();
    }
}
