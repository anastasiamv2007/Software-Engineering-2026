using Microsoft.EntityFrameworkCore;
using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<Subtask> Subtasks => Set<Subtask>();

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Примусово підключаємося до локальної бази planora_db
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=planora_db;Username=postgres;Password=1234");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}