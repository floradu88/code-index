using Microsoft.EntityFrameworkCore;
using CodeIndex.Domain;

namespace CodeIndex.Infrastructure;

public class CodeIndexDbContext : DbContext
{
    public CodeIndexDbContext(DbContextOptions<CodeIndexDbContext> opt) : base(opt) { }
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<SourceFile> SourceFiles => Set<SourceFile>();
    public DbSet<FileChunk> FileChunks => Set<FileChunk>();
    public DbSet<ThreadMessage> ThreadMessages => Set<ThreadMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Project>().HasIndex(x => x.BasePath);
        b.Entity<SourceFile>().HasIndex(x => new { x.ProjectId, x.RelativePath }).IsUnique();
        b.Entity<SourceFile>().Property(x => x.Status).HasConversion<string>();
        b.Entity<Project>().Property(x => x.Status).HasConversion<string>();
        base.OnModelCreating(b);
    }
}
