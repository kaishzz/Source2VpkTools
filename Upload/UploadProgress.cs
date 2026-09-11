namespace Source2VpkDump;

internal sealed record UploadProgress(string Stage, string? Path, long ProcessedBytes, long TotalBytes);
