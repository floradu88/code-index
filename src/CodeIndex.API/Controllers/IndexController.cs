using CodeIndex.Domain;
using CodeIndex.Infrastructure;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CodeIndex.API.Controllers;

[ApiController]
[Route("api/index")]
public class IndexController : ControllerBase
{
    private readonly CodeIndexDbContext _db;
    private readonly IBackgroundJobClient _jobs;

    public IndexController(CodeIndexDbContext db, IBackgroundJobClient jobs)
    { _db = db; _jobs = jobs; }

    public record StartIndexRequest(string Name, string BasePath);

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartIndexRequest req)
    {
        var p = new Project { Name = req.Name, BasePath = Path.GetFullPath(req.BasePath) };
        _db.Projects.Add(p);
        await _db.SaveChangesAsync();

        _jobs.Enqueue<CodeIndex.Workers.IndexProjectJob>(j => j.RunAsync(p.Id, CancellationToken.None));

        return Ok(new { projectId = p.Id, status = p.Status, basePath = p.BasePath });
    }

    [HttpGet("status/{projectId:guid}")]
    public async Task<IActionResult> Status(Guid projectId)
    {
        var p = await _db.Projects
            .Include(x => x.Files).ThenInclude(f => f.Chunks)
            .SingleOrDefaultAsync(x => x.Id == projectId);
        if (p == null) return NotFound();

        return Ok(new {
            p.Id, p.Name, p.BasePath, p.Status,
            files = p.Files.Select(f => new { f.RelativePath, f.Status, chunks = f.Chunks.Count })
        });
    }
}
