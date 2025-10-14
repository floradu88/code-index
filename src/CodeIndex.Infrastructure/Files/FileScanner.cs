using System.Security.Cryptography;
using Microsoft.Extensions.FileSystemGlobbing;

namespace CodeIndex.Infrastructure.Files;

public static class FileScanner
{
    public static List<string> GetFiles(string basePath, IEnumerable<string> include, IEnumerable<string> exclude)
    {
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        foreach (var i in include) matcher.AddInclude(i);
        foreach (var e in exclude) matcher.AddExclude(e);
        return matcher.GetResultsInFullPath(basePath).ToList();
    }

    public static string ComputeSha256(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(fs));
    }

    public static IEnumerable<string> Chunk(string text, int maxChars, int overlap)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        for (int i = 0; i < text.Length; i += maxChars - overlap)
        {
            var len = Math.Min(maxChars, text.Length - i);
            yield return text.Substring(i, len);
            if (i + len >= text.Length) yield break;
        }
    }

    public static bool LooksBinary(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return new[] { ".dll",".exe",".png",".jpg",".jpeg",".gif",".pdf",".zip",".woff",".woff2",".ttf" }.Contains(ext);
    }
}
