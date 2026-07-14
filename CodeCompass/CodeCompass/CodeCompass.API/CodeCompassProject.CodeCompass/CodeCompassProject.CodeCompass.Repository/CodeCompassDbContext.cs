using CodeCompassProject.CodeCompass.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeCompassProject.CodeCompass.Repository;

public class CodeCompassDbContext : DbContext
{
    public CodeCompassDbContext(DbContextOptions<CodeCompassDbContext> options) : base(options)
    {
    }

    public DbSet<ChatSession> ChatSessions { get; set; } = null!;
    public DbSet<ChatMessage> ChatMessages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasMany(e => e.Messages)
                  .WithOne()
                  .HasForeignKey(m => m.SessionId);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).HasConversion<string>();
            entity.Ignore(e => e.Citations); // Stored as JSON or in separate table if needed
        });
    }
}
