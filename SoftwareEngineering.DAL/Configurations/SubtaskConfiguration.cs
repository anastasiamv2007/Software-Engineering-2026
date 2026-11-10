using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Configurations;

public class SubtaskConfiguration : IEntityTypeConfiguration<Subtask>
{
    public void Configure(EntityTypeBuilder<Subtask> builder)
    {
        builder.ToTable("subtasks");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title).IsRequired().HasMaxLength(200);
        builder.Property(s => s.IsCompleted).HasDefaultValue(false);

        builder.HasOne(s => s.Task)
            .WithMany(t => t.Subtasks)
            .HasForeignKey(s => s.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}