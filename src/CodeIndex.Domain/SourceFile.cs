namespace CodeIndex.Domain;

public class SourceFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = default!;
    public string RelativePath { get; set; } = default!;
    public string Sha256 { get; set; } = default!;
    public long SizeBytes { get; set; }
    public string? Language { get; set; }
    public FileIndexStatus Status { get; set; } = FileIndexStatus.Pending;
    public ICollection<FileChunk> Chunks { get; set; } = new List<FileChunk>();
}
