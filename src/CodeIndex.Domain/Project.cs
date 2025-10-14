namespace CodeIndex.Domain;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string BasePath { get; set; } = default!;
    public string? ThreadId { get; set; }
    public string? AssistantId { get; set; }   // <— NEW
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public ProjectStatus Status { get; set; } = ProjectStatus.Pending;
    public ICollection<SourceFile> Files { get; set; } = new List<SourceFile>();
}
