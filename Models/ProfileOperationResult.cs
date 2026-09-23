namespace StartFlow.Models;

public sealed class ProfileOperationResult
{
    private ProfileOperationResult(bool success, string? error)
    {
        Success = success;
        Error = error;
    }

    public bool Success { get; }

    public string? Error { get; }

    public static ProfileOperationResult SuccessResult { get; } = new(true, null);

    public static ProfileOperationResult Fail(string error) => new(false, error);
}

public sealed class ImportProfileResult
{
    public bool Success { get; set; }

    public string? Error { get; set; }

    public string? ImportedName { get; set; }

    /// <summary>Импортированное/исходное имя совпадает с уже существующим профилем.</summary>
    public bool NameConflict { get; set; }

    public static ImportProfileResult Fail(string error) => new()
    {
        Success = false,
        Error = error
    };
}