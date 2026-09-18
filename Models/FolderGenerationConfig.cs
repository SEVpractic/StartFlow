namespace StartFlow.Models;

public sealed class FolderGenerationConfig
{
    public bool OpenAfterCreate { get; set; } = true;

    public bool Enabled { get; set; }

    public string Path { get; set; } = string.Empty;

    public string Template { get; set; } = "Homework {date}";
}