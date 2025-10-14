using System.Text.Json;

namespace CodeIndex.Infrastructure.OpenAI;

public partial class OpenAIChatClient
{
    // ----- DTOs -----
    public record Assistant(string id, string? name, JsonElement? metadata);
}

