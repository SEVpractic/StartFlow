namespace StartFlow.Models;

public sealed class StartupError
{
    public StartupError(string operation, string message)
    {
        Operation = operation;
        Message = message;
    }

    public string Operation { get; }

    public string Message { get; }

    public override string ToString() => $"{Operation}: {Message}";
}

public sealed class StartupResult
{
    public int CreatedFolders { get; set; }

    public int OpenedFolders { get; set; }

    public int StartedPrograms { get; set; }

    public int SkippedPrograms { get; set; }

    public List<StartupError> Errors { get; } = new();

    public bool HasErrors => Errors.Count > 0;

    public void AddError(string operation, string message) => Errors.Add(new StartupError(operation, message));
}