namespace CodeIndex.Infrastructure.OpenAI;

public interface IOpenAIChatClient
{
    Task<string> EnsureThreadAsync(string? threadId, CancellationToken ct);
    Task<string> PostMessageAsync(string threadId, string role, string content, Dictionary<string, string>? metadata, CancellationToken ct);
    Task<string> AskAsync(string threadId, string question, CancellationToken ct); // returns assistant reply text

    // NEW: clean API to run with a specific assistant id
    Task<string> AskWithAssistantAsync(string threadId, string assistantId, string question, CancellationToken ct);
}