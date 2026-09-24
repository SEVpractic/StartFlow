namespace StartFlow.Services;

/// <summary>
/// Пути пользовательских данных приложения. Данные не зависят от расположения exe.
/// </summary>
public static class AppDataPaths
{
    private static readonly Lazy<string> RootLazy = new(() =>
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(basePath))
        {
            basePath = Path.Combine(AppContext.BaseDirectory, "AppData");
        }

        return Path.Combine(basePath, "StartFlow");
    });

    public static string Root => RootLazy.Value;

    public static string ProfilesDirectory => Path.Combine(Root, "profiles");

    public static string BackupsDirectory => Path.Combine(Root, "backups");

    public static string LogsDirectory => Path.Combine(Root, "logs");

    public static string StateFilePath => Path.Combine(Root, "state.json");
}