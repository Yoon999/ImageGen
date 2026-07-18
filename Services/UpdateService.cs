using System.Diagnostics;
using System.IO;

namespace ImageGen.Services;

public sealed class UpdateService
{
    private readonly string? _repositoryRoot = FindRepositoryRoot();

    public string? BuildScriptPath => _repositoryRoot == null
        ? null
        : Path.Combine(_repositoryRoot, "build.bat");

    public async Task<bool> IsUpdateAvailableAsync()
    {
        if (_repositoryRoot == null) return false;

        var fetch = await RunGitAsync("fetch", "origin", "main", "--quiet");
        if (fetch.ExitCode != 0) return false;

        var comparison = await RunGitAsync("rev-list", "--count", "HEAD..origin/main");
        return comparison.ExitCode == 0
               && int.TryParse(comparison.Output.Trim(), out int commitsBehind)
               && commitsBehind > 0;
    }

    private async Task<(int ExitCode, string Output)> RunGitAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = _repositoryRoot!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null) return (-1, string.Empty);

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await process.WaitForExitAsync(timeout.Token);
            return (process.ExitCode, await outputTask);
        }
        catch
        {
            return (-1, string.Empty);
        }
    }

    private static string? FindRepositoryRoot()
    {
        foreach (string startPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(startPath);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                    && File.Exists(Path.Combine(directory.FullName, "build.bat")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }
}
