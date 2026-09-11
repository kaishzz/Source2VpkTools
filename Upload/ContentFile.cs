using System.Security.Cryptography;

namespace Source2VpkDump;

internal sealed record ContentFile(string FullPath, string RelativePath, long Length, string Sha256)
{
    public static ContentFile FromPath(string fullPath, string relativePath)
    {
        fullPath = Path.GetFullPath(fullPath);
        var fileInfo = new FileInfo(fullPath);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var sha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        return new ContentFile(fullPath, relativePath, fileInfo.Length, sha256);
    }
}
