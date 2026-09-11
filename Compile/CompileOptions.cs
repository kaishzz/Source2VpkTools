namespace Source2VpkTools;

internal sealed record CompileOptions(
    string InputPath,
    string? Cs2Root,
    string? ResourceCompilerPath,
    string? GameInfoPath,
    bool Recursive,
    bool Force,
    bool NoVpk,
    bool DryRun);
