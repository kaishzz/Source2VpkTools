namespace Source2VpkTools;

internal sealed record WorkshopUploadRequest(
    ulong? WorkshopId,
    string ContentDirectory,
    string PreviewPath,
    string Title,
    string Description,
    string ChangeNote,
    UploadVisibility Visibility,
    IReadOnlyList<string> Tags);
