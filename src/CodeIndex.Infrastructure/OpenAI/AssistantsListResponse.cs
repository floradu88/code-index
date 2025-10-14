namespace CodeIndex.Infrastructure.OpenAI;

public partial class OpenAIChatClient
{
    public record AssistantsListResponse(List<Assistant> data, string? first_id, string? last_id, bool has_more);
}

