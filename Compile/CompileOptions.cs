namespace Source2VpkTools;

internal sealed record CompileOptions(
    string InputPath,
    string? Cs2Root,
    string? ResourceCompilerPath,
    string? GameInfoPath,
    string? OutputDirectory,
    bool Recursive,
    bool Force,
    bool NoVpk,
    bool NoP4,
    bool Verbose,
    bool DryRun);
