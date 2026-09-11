namespace Source2VpkTools;

internal sealed record WorkshopUploadResult(
    ulong WorkshopId,
    bool WasCreated,
    bool Success,
    string SteamResultCode,
    string SteamResultMessage);
