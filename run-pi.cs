#!/usr/bin/env -S dotnet --
using System.Diagnostics;
using System.Runtime.CompilerServices;

const string ImageName = "pi";
const string WorkspacePath = "/workspace";
const string ConfigPath = "/root/.pi/";

string[] ConfigFiles = ["agent/models.json", "agent/APPEND_SYSTEM.md", "agent/settings.json"];
string[] ExcludedDirectories = [".idea", ".vs", "node_modules", "obj", "bin"];

var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, _) => cancellation.Cancel();

var forceBuild = args.Contains("--force-build");

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
        .. ConfigFiles.Select(file => MakeConfigBind(file, scriptDir))
    ];

    return await RunImageAsync(volumes, cancellation.Token);
}
catch (Exception e)
{
    return e is InvalidOperationException ? 1 : 255;
}

string ResolveScriptDirectory([CallerFilePath] string? path = null)
{
    if (path is null)
    {
        throw new ApplicationException("Cannot determine path to script itself, aborting.\nThis is not your fault.");
    }

    var fileInfo = new FileInfo(path);
    var realPath = fileInfo.LinkTarget ?? fileInfo.FullName;

    return Path.GetDirectoryName(realPath) ?? "/";
}

async Task BuildImageAsync(string scriptDirectory, CancellationToken cancellationToken)
{
    var buildArgs = new[]
    {
        "--pull",
        "-t", ImageName,
        "-f", Path.Combine(scriptDirectory, "Dockerfile")
    };

    var extraArgs = forceBuild ? new[] { "--no-cache" } : [];

    var buildResult = await Process.RunAndCaptureTextAsync(
        fileName: "/usr/bin/env",
        arguments: ["-S", "docker", "--", "build", ..buildArgs, ..extraArgs, scriptDirectory],
        cancellationToken: cancellationToken);

    if (buildResult.ExitStatus.ExitCode is not 0)
    {
        throw new InvalidOperationException($"Failed to build image.\n{buildResult.StandardOutput}");
    }
}

IEnumerable<string> EnumerateIgnoredPaths()
{
    var directoryQueue = new Queue<DirectoryInfo>();
    directoryQueue.Enqueue(new DirectoryInfo(Environment.CurrentDirectory));

    while (directoryQueue.TryDequeue(out var directory))
    {
        foreach (var subdirectory in directory.GetDirectories())
        {
            if (ExcludedDirectories.Contains(subdirectory.Name))
            {
                yield return subdirectory.FullName;
                continue;
            }

            directoryQueue.Enqueue(subdirectory);
        }
    }
}

string MakeWorkspacePath(string path)
{
    var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, path);
    return Path.Combine(WorkspacePath, relativePath);
}

string MakeConfigBind(string file, string scriptDirectory)
{
    return $"{Path.Combine(scriptDirectory, file)}:{Path.Combine(ConfigPath, file)}:ro";
}

async Task<int> RunImageAsync(string[] volumes, CancellationToken cancellationToken)
{
    var environmentArgs = new[]
    {
        "-e", "LLAMA_BASE_URL=http://host.docker.internal:9931",
        "-e", "LLAMA_API_KEY=unused"
    };

    var volumeArgs = volumes.SelectMany(v => new[] { "--volume", v });

    var runResult = await Process.RunAsync(
        fileName: "/usr/bin/env",
        arguments: ["-S", "docker", "--", "run", "--rm", "-it", ..environmentArgs, ..volumeArgs, "--name", "Pi", ImageName],
        cancellationToken: cancellationToken);

    return runResult.ExitCode;
}
