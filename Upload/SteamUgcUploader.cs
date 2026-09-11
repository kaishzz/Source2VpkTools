using Steamworks;

namespace Source2VpkDump;

internal sealed class SteamUgcUploader : IWorkshopUploader
{
    public async Task<WorkshopUploadResult> UploadAsync(
        WorkshopUploadRequest request,
        IProgress<UploadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var nativeLibraryPath = Path.Combine(AppContext.BaseDirectory, "steam_api64.dll");
        if (!File.Exists(nativeLibraryPath))
        {
            throw new FileNotFoundException("steam_api64.dll was not found beside vpkdump.exe", nativeLibraryPath);
        }

        if (!SteamAPI.Init())
        {
            throw new InvalidOperationException("SteamAPI initialization failed; start Steam and ensure steam_appid.txt contains 730");
        }

        try
        {
            if (!SteamUser.BLoggedOn())
            {
                throw new InvalidOperationException("The current Steam client is not logged in");
            }

            if ((uint)SteamUtils.GetAppID() != WorkshopConstants.Cs2AppId)
            {
                throw new InvalidOperationException($"Steam initialized App ID {(uint)SteamUtils.GetAppID()}, but CS2 requires {WorkshopConstants.Cs2AppId}");
            }

            return await UploadCoreAsync(request, progress, cancellationToken);
        }
        finally
        {
            SteamAPI.Shutdown();
        }
    }

    private static async Task<WorkshopUploadResult> UploadCoreAsync(
        WorkshopUploadRequest request,
        IProgress<UploadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var appId = new AppId_t(WorkshopConstants.Cs2AppId);
        var workshopId = request.WorkshopId is ulong existingId
            ? new PublishedFileId_t(existingId)
            : default;
        var wasCreated = false;

        if (request.WorkshopId is null)
        {
            var createHandle = SteamUGC.CreateItem(appId, EWorkshopFileType.k_EWorkshopFileTypeCommunity);
            var createResult = await WaitForCallResultAsync<CreateItemResult_t>(createHandle, cancellationToken);
            if (createResult.IoFailure || createResult.Callback.m_eResult != EResult.k_EResultOK)
            {
                return FailedResult(createResult.Callback.m_eResult, "CreateItem failed");
            }

            workshopId = createResult.Callback.m_nPublishedFileId;
            wasCreated = true;
        }

        var updateHandle = SteamUGC.StartItemUpdate(appId, workshopId);
        if (updateHandle == UGCUpdateHandle_t.Invalid)
        {
            throw new InvalidOperationException("SteamUGC.StartItemUpdate returned an invalid handle");
        }

        SetRequired(SteamUGC.SetItemTitle(updateHandle, request.Title), "title");
        SetRequired(SteamUGC.SetItemDescription(updateHandle, request.Description), "description");
        SetRequired(SteamUGC.SetItemVisibility(updateHandle, ToSteamVisibility(request.Visibility)), "visibility");
        SetRequired(SteamUGC.SetItemContent(updateHandle, request.ContentDirectory), "content");
        SetRequired(SteamUGC.SetItemPreview(updateHandle, request.PreviewPath), "preview");
        if (request.Tags.Count > 0)
        {
            SetRequired(SteamUGC.SetItemTags(updateHandle, request.Tags.ToArray()), "tags");
        }

        var submitHandle = SteamUGC.SubmitItemUpdate(updateHandle, request.ChangeNote);
        var submitResult = await WaitForSubmitResultAsync(submitHandle, updateHandle, progress, cancellationToken);
        if (submitResult.IoFailure || submitResult.Callback.m_eResult != EResult.k_EResultOK)
        {
            return new WorkshopUploadResult(
                (ulong)workshopId,
                wasCreated,
                false,
                submitResult.IoFailure ? "IOFailure" : submitResult.Callback.m_eResult.ToString(),
                "SubmitItemUpdate failed");
        }

        return new WorkshopUploadResult(
            (ulong)workshopId,
            wasCreated,
            true,
            submitResult.Callback.m_eResult.ToString(),
            "OK");
    }

    private static async Task<(T Callback, bool IoFailure)> WaitForCallResultAsync<T>(
        SteamAPICall_t handle,
        CancellationToken cancellationToken)
        where T : struct
    {
        var completion = new TaskCompletionSource<(T Callback, bool IoFailure)>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var callResult = CallResult<T>.Create((callback, ioFailure) => completion.TrySetResult((callback, ioFailure)));
        callResult.Set(handle);

        while (!completion.Task.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SteamAPI.RunCallbacks();
            await Task.Delay(50, cancellationToken);
        }

        return await completion.Task;
    }

    private static async Task<(SubmitItemUpdateResult_t Callback, bool IoFailure)> WaitForSubmitResultAsync(
        SteamAPICall_t handle,
        UGCUpdateHandle_t updateHandle,
        IProgress<UploadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<(SubmitItemUpdateResult_t Callback, bool IoFailure)>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var callResult = CallResult<SubmitItemUpdateResult_t>.Create((callback, ioFailure) => completion.TrySetResult((callback, ioFailure)));
        callResult.Set(handle);

        EItemUpdateStatus? lastStatus = null;
        while (!completion.Task.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SteamAPI.RunCallbacks();
            var status = SteamUGC.GetItemUpdateProgress(updateHandle, out var bytesProcessed, out var bytesTotal);
            if (lastStatus != status || bytesProcessed > 0)
            {
                progress?.Report(new UploadProgress(
                    $"Uploading ({status})",
                    null,
                    checked((long)Math.Min(bytesProcessed, long.MaxValue)),
                    checked((long)Math.Min(bytesTotal, long.MaxValue))));
                lastStatus = status;
            }

            await Task.Delay(250, cancellationToken);
        }

        return await completion.Task;
    }

    private static ERemoteStoragePublishedFileVisibility ToSteamVisibility(UploadVisibility visibility) => visibility switch
    {
        UploadVisibility.Public => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic,
        UploadVisibility.FriendsOnly => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly,
        UploadVisibility.Private => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate,
        UploadVisibility.Unlisted => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted,
        _ => throw new ArgumentOutOfRangeException(nameof(visibility))
    };

    private static void SetRequired(bool result, string field)
    {
        if (!result)
        {
            throw new InvalidOperationException($"Steam rejected the Workshop {field} value");
        }
    }

    private static WorkshopUploadResult FailedResult(EResult result, string message) =>
        new(0, false, false, result.ToString(), message);
}
