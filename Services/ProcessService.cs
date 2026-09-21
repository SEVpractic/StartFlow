using System.Diagnostics;
using StartFlow.Models;

namespace StartFlow.Services;

public enum ProgramStartResult
{
    Started,
    Skipped
}

public sealed class ProcessService
{
    public bool IsProcessRunning(string processName)
    {
        var name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return Process.GetProcessesByName(name).Length > 0;
    }

    public ProgramStartResult Start(ProgramConfig program)
    {
        if (program.SkipIfRunning
            && !string.IsNullOrWhiteSpace(program.ProcessName)
            && IsProcessRunning(program.ProcessName))
        {
            return ProgramStartResult.Skipped;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = program.Path,
            UseShellExecute = true
        };

        var workingDirectory = ResolveWorkingDirectory(program);
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        if (!string.IsNullOrWhiteSpace(program.Args))
        {
            startInfo.Arguments = program.Args;
        }

        if (program.RunAsAdmin)
        {
            startInfo.Verb = "runas";
        }

        Process.Start(startInfo);
        return ProgramStartResult.Started;
    }

    private static string? ResolveWorkingDirectory(ProgramConfig program)
    {
        if (!string.IsNullOrWhiteSpace(program.WorkingDirectory))
        {
            return program.WorkingDirectory;
        }

        var directory = Path.GetDirectoryName(program.Path);
        return string.IsNullOrWhiteSpace(directory) ? null : directory;
    }
}