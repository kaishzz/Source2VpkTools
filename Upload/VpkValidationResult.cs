namespace Source2VpkDump;

internal sealed record VpkValidationResult(
    IReadOnlyList<string> MissingPaths,
    IReadOnlyList<string> UnexpectedPaths,
    IReadOnlyList<string> SizeMismatches,
    IReadOnlyList<string> HashMismatches)
{
    public bool IsSuccess => MissingPaths.Count == 0 &&
                             UnexpectedPaths.Count == 0 &&
                             SizeMismatches.Count == 0 &&
                             HashMismatches.Count == 0;
}
