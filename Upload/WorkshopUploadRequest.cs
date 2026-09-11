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

internal interface IWorkshopUploader
{
    Task<WorkshopUploadResult> UploadAsync(
        WorkshopUploadRequest request,
        IProgress<UploadProgress>? progress,
        CancellationToken cancellationToken);
}
