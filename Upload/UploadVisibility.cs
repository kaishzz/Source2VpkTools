namespace Source2VpkTools;

internal enum UploadVisibility
{
    Public,
    FriendsOnly,
    Private,
    Unlisted
}

internal static class UploadVisibilityParser
{
    public static bool TryParse(string value, out UploadVisibility visibility)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "public":
                visibility = UploadVisibility.Public;
                return true;
            case "friends":
                visibility = UploadVisibility.FriendsOnly;
                return true;
            case "private":
                visibility = UploadVisibility.Private;
                return true;
            case "unlisted":
                visibility = UploadVisibility.Unlisted;
                return true;
            default:
                visibility = default;
                return false;
        }
    }
}
