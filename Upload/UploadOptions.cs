namespace Source2VpkTools;

internal sealed record UploadOptions(
    string? ContentDirectory,
    string? VpkPath,
    string? PackageName,
    ulong? WorkshopId,
    string Title,
    string PreviewPath,
    string? DescriptionFilePath,
    string ChangeNote,
    UploadVisibility Visibility,
    IReadOnlyList<string> Tags,
    bool DryRun,
    bool KeepStaging,
    string? StateFilePath);
