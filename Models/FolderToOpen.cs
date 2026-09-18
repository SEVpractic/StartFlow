namespace StartFlow.Models;

public sealed class FolderToOpen
{
    public string Path { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}