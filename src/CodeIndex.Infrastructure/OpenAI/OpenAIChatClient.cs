using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace CodeIndex.Infrastructure.OpenAI;

public partial class OpenAIChatClient : IOpenAIChatClient
{
    protected readonly HttpClient _http;
    private readonly string _assistantId;
    private readonly string _model;
    private readonly bool _useAzure;
    private readonly string _apiVersion;

    public OpenAIChatClient(IConfiguration cfg, IHttpClientFactory f)
    {
        _http = f.CreateClient(nameof(OpenAIChatClient));

        var baseUrl = cfg["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1";
        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        _http.BaseAddress = new Uri(baseUrl);

        var apiKey = cfg["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OpenAI:ApiKey is missing.");

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        // Optional scoping
        var org = cfg["OpenAI:Organization"];
        if (!string.IsNullOrWhiteSpace(org))
            _http.DefaultRequestHeaders.Add("OpenAI-Organization", org);

        var project = cfg["OpenAI:Project"];
        if (!string.IsNullOrWhiteSpace(project))
            _http.DefaultRequestHeaders.Add("OpenAI-Project", project);

        _assistantId = cfg["OpenAI:AssistantId"] ?? "";
        _model = cfg["OpenAI:Model"] ?? "gpt-4.1";

        // Detect Azure by baseUrl host
        _useAzure = new Uri(baseUrl).Host.Contains("openai.azure.com", StringComparison.OrdinalIgnoreCase);
        _apiVersion = cfg["OpenAI:ApiVersion"] ?? "2024-07-01-preview"; // Azure requires version

        // OpenAI Assistants v2 header (NOT for Azure)
        if (!_useAzure)
            _http.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v2");
    }

    // Add this helper in OpenAIChatClient
    private string? _cachedAssistantId;
    private async Task<string> EnsureAssistantAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_assistantId)) return _assistantId!;
        if (!string.IsNullOrWhiteSpace(_cachedAssistantId)) return _cachedAssistantId!;

        var payload = new
        {
            model = _model,
            name = "CodeIndex Assistant",
            instructions = "You help answer questions about a codebase whose files are posted as messages with metadata file & chunkIndex. Prefer precise, scoped answers; be concise."
        };

        var endpoint = PathWithApi(_useAzure ? "assistants" : "assistants");
        // Standard OpenAI requires the v2 beta header; we already set it in ctor when not Azure.

        var res = await _http.PostAsync(endpoint,
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
        await EnsureAsync(res);

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        _cachedAssistantId = doc.RootElement.GetProperty("id").GetString();
        return _cachedAssistantId!;
    }

    private string PathWithApi(string path)
    {
        if (_useAzure)
        {
            // Azure Assistants path shape: /openai/assistants/v2/threads?api-version=...
            var p = path.StartsWith("threads") ? $"openai/assistants/v2/{path}" : path;
            return $"{p}{(p.Contains('?') ? "&" : "?")}api-version={_apiVersion}";
        }
        return path; // Standard OpenAI
    }

    private static async Task<HttpResponseMessage> EnsureAsync(HttpResponseMessage res)
    {
        if (res.IsSuccessStatusCode) return res;

        var body = await res.Content.ReadAsStringAsync();
        var msg = $"OpenAI HTTP {(int)res.StatusCode} {res.ReasonPhrase}. Body: {body}";
        throw new HttpRequestException(msg);
    }

    public async Task<string> EnsureThreadAsync(string? threadId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(threadId)) return threadId!;

        var endpoint = PathWithApi("threads");
        var res = await _http.PostAsync(endpoint,
            new StringContent("{}", Encoding.UTF8, "application/json"), ct);

        await EnsureAsync(res);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    public async Task<string> PostMessageAsync(string threadId, string role, string content,
        Dictionary<string, string>? metadata, CancellationToken ct)
    {
        var payload = new { role, content, metadata };
        var endpoint = PathWithApi($"threads/{threadId}/messages");
        var res = await _http.PostAsync(endpoint,
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);

        await EnsureAsync(res);
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return json.RootElement.GetProperty("id").GetString()!;
    }

    public async Task<string> AskAsync(string threadId, string question, CancellationToken ct)
    {
        // 1) add user question
        _ = await PostMessageAsync(threadId, "user", question, null, ct);

        // 2) ensure assistant id
        var asst = await EnsureAssistantAsync(ct);

        // 3) run the assistant on the thread
        var body = new { assistant_id = asst };
        var runEndpoint = PathWithApi($"threads/{threadId}/runs");
        var run = await _http.PostAsync(runEndpoint,
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);
        await EnsureAsync(run);

        string? runId;
        using (var doc = JsonDocument.Parse(await run.Content.ReadAsStringAsync(ct)))
            runId = doc.RootElement.GetProperty("id").GetString();

        // 3) poll
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            var s = await _http.GetAsync(PathWithApi($"threads/{threadId}/runs/{runId}"), ct);
            await EnsureAsync(s);
            using var sdoc = JsonDocument.Parse(await s.Content.ReadAsStringAsync(ct));
            var status = sdoc.RootElement.GetProperty("status").GetString();
            if (status is "completed" or "failed" or "cancelled" or "expired")
                break;
        }

        // 4) read latest assistant message
        var msgs = await _http.GetAsync(PathWithApi($"threads/{threadId}/messages?limit=1&order=desc"), ct);
        await EnsureAsync(msgs);
        using var mdoc = JsonDocument.Parse(await msgs.Content.ReadAsStringAsync(ct));
        var data = mdoc.RootElement.GetProperty("data")[0];
        var role = data.GetProperty("role").GetString();
        var text = data.GetProperty("content")[0].GetProperty("text").GetProperty("value").GetString();
        return role == "assistant" ? text ?? "" : "";
    }

    public async Task<string> AskWithAssistantAsync(string threadId, string assistantId, string question, CancellationToken ct)
    {
        // 1) add user question
        await PostMessageAsync(threadId, "user", question, null, ct);

        // 2) create a run bound to the explicit assistant
        var body = new { assistant_id = assistantId };
        var runEndpoint = PathWithApi($"threads/{threadId}/runs");
        var run = await _http.PostAsync(runEndpoint,
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);
        await EnsureAsync(run);

        string? runId;
        using (var doc = JsonDocument.Parse(await run.Content.ReadAsStringAsync(ct)))
            runId = doc.RootElement.GetProperty("id").GetString();

        // 3) poll until done
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            var s = await _http.GetAsync(PathWithApi($"threads/{threadId}/runs/{runId}"), ct);
            await EnsureAsync(s);
            using var sdoc = JsonDocument.Parse(await s.Content.ReadAsStringAsync(ct));
            var status = sdoc.RootElement.GetProperty("status").GetString();
            if (status is "completed" or "failed" or "cancelled" or "expired") break;
        }

        // 4) grab latest assistant message
        var msgs = await _http.GetAsync(PathWithApi($"threads/{threadId}/messages?limit=1&order=desc"), ct);
        await EnsureAsync(msgs);
        using var mdoc = JsonDocument.Parse(await msgs.Content.ReadAsStringAsync(ct));
        var data = mdoc.RootElement.GetProperty("data")[0];
        var role = data.GetProperty("role").GetString();
        var text = data.GetProperty("content")[0].GetProperty("text").GetProperty("value").GetString();
        return role == "assistant" ? (text ?? "") : "";
    }


    // ----- Helpers for endpoint paths (already in your class) -----
    // PathWithApi(string path) // handles Azure vs OpenAI
    // EnsureAsync(HttpResponseMessage res)

    // ----- List assistants with pagination -----
    public async IAsyncEnumerable<Assistant> ListAssistantsAsync([EnumeratorCancellation] CancellationToken ct)
    {
        // OpenAI: GET /assistants?limit=100 (with OpenAI-Beta: assistants=v2)
        // Azure:  GET /openai/assistants/v2/assistants?api-version=...
        var endpoint = PathWithApi("assistants");
        string? after = null;

        while (true)
        {
            var url = endpoint + (endpoint.Contains("?") ? "&" : "?") + "limit=100" + (after is null ? "" : $"&after={after}");
            var res = await _http.GetAsync(url, ct);
            await EnsureAsync(res);
            var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var root = json.RootElement;

            var data = new List<Assistant>();
            foreach (var el in root.GetProperty("data").EnumerateArray())
            {
                JsonElement? meta = null;
                if (el.TryGetProperty("metadata", out var m)) meta = m;
                data.Add(new Assistant(
                    el.GetProperty("id").GetString()!,
                    el.TryGetProperty("name", out var n) ? n.GetString() : null,
                    meta));
            }

            foreach (var a in data) yield return a;

            var hasMore = root.TryGetProperty("has_more", out var hm) && hm.GetBoolean();
            if (!hasMore) yield break;
            after = root.TryGetProperty("last_id", out var lid) ? lid.GetString() : null;
            if (string.IsNullOrEmpty(after)) yield break;
        }
    }

    // ----- Create assistant (tag with metadata.projectId) -----
    public async Task<string> CreateAssistantAsync(Guid projectId, string model, string name, string instructions, CancellationToken ct)
    {
        var payload = new
        {
            model,
            name,
            instructions,
            metadata = new Dictionary<string, string>
            {
                ["projectId"] = projectId.ToString()
            }
        };

        var res = await _http.PostAsync(PathWithApi("assistants"),
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
        await EnsureAsync(res);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    // ----- Ensure assistant for project: find by metadata.projectId, else create -----
    public async Task<string> EnsureAssistantForProjectAsync(Guid projectId, string defaultName, string defaultInstructions, CancellationToken ct)
    {
        // 1) Search by metadata.projectId (exact match)
        await foreach (var a in ListAssistantsAsync(ct))
        {
            try
            {
                if (a.metadata.HasValue &&
                    a.metadata.Value.ValueKind == JsonValueKind.Object &&
                    a.metadata.Value.TryGetProperty("projectId", out var pid) &&
                    Guid.TryParse(pid.GetString(), out var g) &&
                    g == projectId)
                {
                    return a.id;
                }
            }
            catch { /* ignore malformed metadata */ }
        }

        // 2) Not found → create one (use configured model or AssistantId if set)
        string model = !string.IsNullOrWhiteSpace(_assistantId) ? _model : _model; // model is required when creating assistant
        var name = string.IsNullOrWhiteSpace(defaultName) ? $"CodeIndex Assistant ({projectId})" : defaultName;
        var instructions = string.IsNullOrWhiteSpace(defaultInstructions)
            ? "You answer questions about a codebase whose files are posted as messages (with metadata file & chunkIndex). Be precise and reference file paths when relevant."
            : defaultInstructions;

        return await CreateAssistantAsync(projectId, model, name, instructions, ct);
    }
}

