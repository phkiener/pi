#!/usr/bin/env -S dotnet --
using System.Diagnostics;
using System.Runtime.CompilerServices;

const string ImageName = "pi";
const string WorkspacePath = "/workspace";
const string ConfigPath = "/root/.pi/";
string[] ConfigFiles = ["agent/models.json", "agent/APPEND_SYSTEM.md", "agent/settings.json"];

var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, _) => cancellation.Cancel();

try
{
    var scriptDir = ResolveScriptDirectory();
    await BuildImageAsync(scriptDir, cancellation.Token);

    string[] volumes =
    [
        // mount the current directory...
        $"{Environment.CurrentDirectory}:{WorkspacePath}",

        // ... but exclude ignored paths by making anonymous volumes on top
        .. EnumerateIgnoredPaths().Select(MakeWorkspacePath),

        // include the configuration inside as read-only
        .. ConfigFiles.Select(file => $"{Path.Combine(scriptDir, file)}:{Path.Combine(ConfigPath, file)}:ro")
    ];

    return await RunImageAsync(volumes, cancellation.Token);
}
catch (Exception e)
{
    return e is InvalidOperationException ? 1 : 255;
}

static string ResolveScriptDirectory([CallerFilePath] string? path = null)
{
    var scriptDirectory = Path.GetDirectoryName(path);
    if (scriptDirectory is null)
    {
        throw new ApplicationException("Cannot determine path to script itself, aborting.\n This is not your fault.");
    }

    return scriptDirectory;
}

static async Task BuildImageAsync(string scriptDirectory, CancellationToken cancellationToken)
{
    var buildResult = await Process.RunAndCaptureTextAsync(
        fileName: "/usr/bin/env",
        arguments: ["-S", "docker", "--", "build", "--pull", "-t", ImageName, "-f", Path.Combine(scriptDirectory, "Dockerfile"), scriptDirectory],
        cancellationToken: cancellationToken);

    if (buildResult.ExitStatus.ExitCode is not 0)
    {
        throw new InvalidOperationException($"Failed to build image\n{buildResult.StandardOutput}");
    }
}

static IEnumerable<string> EnumerateIgnoredPaths()
{
    var directoryQueue = new Queue<DirectoryInfo>();
    directoryQueue.Enqueue(new DirectoryInfo(Environment.CurrentDirectory));

    while (directoryQueue.TryDequeue(out var directory))
    {
        foreach (var subdirectory in directory.GetDirectories())
        {
            if (subdirectory.Name is "node_modules")
            {
                yield return subdirectory.FullName;
            }

            directoryQueue.Enqueue(subdirectory);
        }
    }
}

static string MakeWorkspacePath(string path)
{
    var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, path);
    return Path.Combine(WorkspacePath, relativePath);
}

static async Task<int> RunImageAsync(string[] volumes, CancellationToken cancellationToken)
{
    var volumeArgs = volumes.SelectMany(v => new[] { "--volume", v });

    var runResult = await Process.RunAsync(
        fileName: "/usr/bin/env",
        arguments: ["-S", "docker", "--", "run", "--rm", "-it", ..volumeArgs, "--name", "Pi", ImageName],
        cancellationToken: cancellationToken);

    return runResult.ExitCode;
}
