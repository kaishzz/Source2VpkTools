namespace Source2VpkTools;

internal sealed record UploadProgress(string Stage, string? Path, long ProcessedBytes, long TotalBytes);
