namespace StartFlow.Models;

public sealed class ProgramConfig
{
    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Args { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public bool RunAsAdmin { get; set; }

    public bool SkipIfRunning { get; set; }

    public string ProcessName { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}