using System.Diagnostics;

namespace Source2VpkTools;

internal static class ResourceCompilerProcess
{
    public static int Run(string compilerPath, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = compilerPath,
            WorkingDirectory = Path.GetDirectoryName(compilerPath)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += static (_, eventArgs) => WriteLine(eventArgs.Data);
        process.ErrorDataReceived += static (_, eventArgs) => WriteLine(eventArgs.Data);
        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start resourcecompiler.exe");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        process.WaitForExit();
        return process.ExitCode;
    }

    private static void WriteLine(string? line)
    {
        if (!string.IsNullOrEmpty(line))
        {
            Console.WriteLine($"[resourcecompiler] {line}");
        }
    }
}
