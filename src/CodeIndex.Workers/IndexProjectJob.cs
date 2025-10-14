using CodeIndex.Domain;
using CodeIndex.Infrastructure;
using CodeIndex.Infrastructure.Files;
using CodeIndex.Infrastructure.OpenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CodeIndex.Workers;

public class IndexProjectJob
{
    private readonly CodeIndexDbContext _db;
    private readonly IOpenAIChatClient _ai;
    private readonly IConfiguration _cfg;
    private readonly ILogger<IndexProjectJob> _log;

    public IndexProjectJob(CodeIndexDbContext db, IOpenAIChatClient ai, IConfiguration cfg, ILogger<IndexProjectJob> log)
    {
        _db = db; _ai = ai; _cfg = cfg; _log = log;
    }

    public async Task RunAsync(Guid projectId, CancellationToken ct = default)
    {
        var p = await _db.Projects.Include(x => x.Files).ThenInclude(f => f.Chunks)
                 .SingleAsync(x => x.Id == projectId, ct);
        p.Status = ProjectStatus.Indexing;
        await _db.SaveChangesAsync(ct);

        p.ThreadId = await _ai.EnsureThreadAsync(p.ThreadId, ct);
        await _db.SaveChangesAsync(ct);

        var include = _cfg.GetSection("Indexing:Include").Get<string[]>() ?? Array.Empty<string>();
        var exclude = _cfg.GetSection("Indexing:Exclude").Get<string[]>() ?? Array.Empty<string>();
        var maxKb = _cfg.GetValue<int>("Indexing:MaxFileSizeKB", 512);
        var max = _cfg.GetValue<int>("Indexing:Chunk:MaxChars", 8000);
        var overlap = _cfg.GetValue<int>("Indexing:Chunk:OverlapChars", 400);

        var all = FileScanner.GetFiles(p.BasePath, include, exclude);
        foreach (var path in all)
        {
            if (FileScanner.LooksBinary(path)) continue;
            var info = new FileInfo(path);
            if (info.Length > maxKb * 1024) continue;

            var rel = Path.GetRelativePath(p.BasePath, path);
            var sha = FileScanner.ComputeSha256(path);

            var sf = p.Files.FirstOrDefault(x => x.RelativePath == rel);
            if (sf == null)
            {
                sf = new SourceFile { ProjectId = p.Id, RelativePath = rel, Sha256 = sha, SizeBytes = info.Length };
                _db.SourceFiles.Add(sf);
                await _db.SaveChangesAsync(ct);
            }
            else if (sf.Sha256 == sha && sf.Status == FileIndexStatus.Uploaded)
            {
                // unchanged; skip
                continue;
            }
            sf.Sha256 = sha;
            sf.Status = FileIndexStatus.Pending;

            // clear previous chunks (if reindexing)
            var existing = _db.FileChunks.Where(c => c.SourceFileId == sf.Id);
            _db.FileChunks.RemoveRange(existing);
            await _db.SaveChangesAsync(ct);

            var text = await File.ReadAllTextAsync(path, ct);
            var idx = 0;
            foreach (var chunk in FileScanner.Chunk(text, max, overlap))
            {
                var fc = new FileChunk { SourceFileId = sf.Id, Index = idx++, Content = chunk };
                _db.FileChunks.Add(fc);
            }
            await _db.SaveChangesAsync(ct);

            List<FileChunk> ordered = [.. _db.FileChunks.Where(x => x.SourceFileId == sf.Id).OrderBy(x => x.Index)];
            foreach (var ch in ordered)
            {
                try
                {
                    var mid = await _ai.PostMessageAsync(
                        p.ThreadId!, "user", ch.Content,
                        new Dictionary<string, string>
                        {
                            ["file"] = rel,
                            ["chunkIndex"] = ch.Index.ToString()
                        }, ct);
                    ch.OpenAIMessageId = mid;
                    ch.UploadedUtc = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    ch.Error = ex.Message;
                    sf.Status = FileIndexStatus.Failed;
                    await _db.SaveChangesAsync(ct);
                    _log.LogError(ex, "Failed uploading chunk {Rel}#{Idx}", rel, ch.Index);
                }
            }
#pragma warning disable S6966 // Awaitable method should be used
            if (_db.FileChunks.Where(x => x.SourceFileId == sf.Id)
                .All(c => c.OpenAIMessageId != null))
            {
                sf.Status = FileIndexStatus.Uploaded;
            }
#pragma warning restore S6966 // Awaitable method should be used

            await _db.SaveChangesAsync(ct);
        }

        p.Status = p.Files.Any(f => f.Status == FileIndexStatus.Failed)
            ? ProjectStatus.Partial
            : ProjectStatus.Completed;
        await _db.SaveChangesAsync(ct);
    }
}
