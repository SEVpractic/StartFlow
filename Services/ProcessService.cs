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
    private static readonly string[] CommandScriptExtensions = { ".cmd", ".bat" };

    /// <summary>Командные скрипты, которые нельзя запускать напрямую через ShellExecute.</summary>
    public static bool IsCommandScript(string path)
        => !string.IsNullOrWhiteSpace(path)
            && CommandScriptExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

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

        var startInfo = CreateStartInfo(program);

        var workingDirectory = ResolveWorkingDirectory(program);
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        if (program.RunAsAdmin)
        {
            startInfo.Verb = "runas";
        }

        Process.Start(startInfo);
        return ProgramStartResult.Started;
    }

    private static ProcessStartInfo CreateStartInfo(ProgramConfig program)
    {
        if (IsCommandScript(program.Path))
        {
            return new ProcessStartInfo
            {
                FileName = ResolveCommandInterpreter(),
                Arguments = BuildCommandScriptArguments(program.Path, program.Args),
                UseShellExecute = true
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = program.Path,
            UseShellExecute = true
        };

        if (!string.IsNullOrWhiteSpace(program.Args))
        {
            startInfo.Arguments = program.Args;
        }

        return startInfo;
    }

    private static string ResolveCommandInterpreter()
    {
        var comSpec = Environment.GetEnvironmentVariable("ComSpec");
        return string.IsNullOrWhiteSpace(comSpec) ? "cmd.exe" : comSpec;
    }

    /// <summary>
    /// Формирует строку аргументов для cmd.exe: путь к скрипту всегда в кавычках,
    /// аргументы пользователя переносятся без изменений, вся команда оборачивается
    /// в кавычки для <c>/s</c>, чтобы cmd не разрезал команду по пробелам в пути.
    /// </summary>
    private static string BuildCommandScriptArguments(string scriptPath, string? args)
    {
        var command = string.IsNullOrWhiteSpace(args)
            ? Quote(scriptPath)
            : $"{Quote(scriptPath)} {args}";

        return $"/d /s /c \"{command}\"";
    }

    private static string Quote(string value) => $"\"{value}\"";

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