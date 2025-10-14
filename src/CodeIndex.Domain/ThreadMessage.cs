namespace CodeIndex.Domain;

public class ThreadMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Role { get; set; } = default!; // "user" | "assistant"
    public string Content { get; set; } = default!;
    public string OpenAIMessageId { get; set; } = default!;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
