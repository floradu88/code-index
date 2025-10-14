using CodeIndex.Domain;
using CodeIndex.Infrastructure;
using CodeIndex.Infrastructure.OpenAI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CodeIndex.API.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly CodeIndexDbContext _db;
    private readonly IOpenAIChatClient _ai;

    public ChatController(CodeIndexDbContext db, IOpenAIChatClient ai)
    { _db = db; _ai = ai; }

    [HttpPost("thread")]
    public async Task<IActionResult> EnsureThread([FromBody] Guid projectId, CancellationToken ct)
    {
        var p = await _db.Projects.FindAsync([projectId], ct);
        if (p == null) return NotFound();
        p.ThreadId = await _ai.EnsureThreadAsync(p.ThreadId, ct);
        await _db.SaveChangesAsync(ct);
        return Ok(new { p.Id, p.ThreadId });
    }

    public record AskRequest(Guid ProjectId, string Question);

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] AskRequest req, CancellationToken ct)
    {
        var p = await _db.Projects.FindAsync([req.ProjectId], ct);
        if (p == null) return NotFound("Project not found");

        p.ThreadId = await _ai.EnsureThreadAsync(p.ThreadId, ct);

        // ensure assistant per project (your ensure-assistant method)
        if (string.IsNullOrWhiteSpace(p.AssistantId))
        {
            var asstId = await (_ai as OpenAIChatClient)!.EnsureAssistantForProjectAsync(
                p.Id, "CodeIndex Assistant", "", ct);
            p.AssistantId = asstId;
            await _db.SaveChangesAsync(ct);
        }

        var answer = await _ai.AskWithAssistantAsync(p.ThreadId!, p.AssistantId!, req.Question, ct);

        _db.ThreadMessages.Add(new ThreadMessage
        {
            ProjectId = p.Id,
            Role = "assistant",
            Content = answer,
            OpenAIMessageId = "cached"
        });
        await _db.SaveChangesAsync(ct);

        return Ok(new { answer });
    }

    [HttpGet("history/{projectId:guid}")]
    public async Task<IActionResult> History(Guid projectId)
    {
        var msgs = await _db.ThreadMessages
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedUtc)
            .Take(50)
            .ToListAsync();
        return Ok(msgs.Select(m => new { m.Role, m.Content, m.CreatedUtc }));
    }

    [HttpPost("assistant/ensure")]
    public async Task<IActionResult> EnsureAssistant([FromBody] EnsureAssistantRequest req, CancellationToken ct)
    {
        var p = await _db.Projects.FindAsync([req.ProjectId], ct);
        if (p == null) return NotFound("Project not found");

        var ai = _ai; // injected
        var asstId = await (ai as dynamic).EnsureAssistantForProjectAsync(req.ProjectId, req.Name ?? "", req.Instructions ?? "", ct);

        p.AssistantId = asstId;
        await _db.SaveChangesAsync(ct);

        return Ok(new { projectId = p.Id, assistantId = asstId });
    }

    public record EnsureAssistantRequest(Guid ProjectId, string? Name, string? Instructions);

}
