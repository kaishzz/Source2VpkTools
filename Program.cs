using System.Diagnostics;

namespace Source2VpkTools;

internal static class Program
{
    public static int Main(string[] args)
    {
        var parseResult = CliOptions.Parse(args);
        if (!parseResult.IsSuccess)
        {
            Console.Error.WriteLine($"Error: {parseResult.Error}");
            Console.Error.WriteLine("Run 'Source2VpkTools --help' for usage.");
            return 2;
        }

        var options = parseResult.Options!;
        if (options.ShowHelp)
        {
            ConsoleReporter.WriteHelp();
            return 0;
        }

        if (options.ShowVersion)
        {
            ConsoleReporter.WriteVersion();
            return 0;
        }

        if (options.Command == CliCommand.Compile)
        {
            return CompileCommand.Run(options.Compile!);
        }

        if (options.Command == CliCommand.Upload)
        {
            return UploadCommand.Run(options.Upload!);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = VpkExtractor.Extract(options.InputPath!, options.OutputDirectory!, ConsoleReporter.WriteEntry);
            stopwatch.Stop();
            ConsoleReporter.WriteSummary(options.InputPath!, result, stopwatch.Elapsed);
            return 0;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            Console.Error.WriteLine($"Error: {exception.Message}");
            return 1;
        }
    }
}
