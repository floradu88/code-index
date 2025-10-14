namespace CodeIndex.Domain;

public class FileChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceFileId { get; set; }
    public SourceFile SourceFile { get; set; } = default!;
    public int Index { get; set; }
    public string Content { get; set; } = default!;
    public string? OpenAIMessageId { get; set; }
    public DateTime? UploadedUtc { get; set; }
    public string? Error { get; set; }
}
