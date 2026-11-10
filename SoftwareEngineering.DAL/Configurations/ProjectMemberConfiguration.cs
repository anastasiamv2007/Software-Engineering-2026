using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Configurations;

public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("project_members");

        builder.HasKey(m => new { m.ProjectId, m.UserId });

        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.DisplayTitle).HasMaxLength(100);

        builder.HasOne(m => m.Project)
            .WithMany(p => p.Members)
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}