using System.Globalization;

namespace StartFlow.Services;

/// <summary>
/// Простые резервные копии: копии JSON-файлов конфигурации со штампами времени
/// в каталоге backups. Создаются только перед потенциально опасными операциями
/// (например, миграции) и при повреждении файлов.
/// </summary>
public sealed class BackupService
{
    public string CreateJsonBackup(string sourceName, int schemaVersion, string json)
    {
        Directory.CreateDirectory(AppDataPaths.BackupsDirectory);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var baseName = Path.GetFileNameWithoutExtension(sourceName);
        var path = Path.Combine(AppDataPaths.BackupsDirectory, $"{baseName}_v{schemaVersion}_{stamp}.json");

        var unique = UniquePath(path);
        File.WriteAllText(unique, json);
        return unique;
    }

    public string CopyFileToBackups(string sourcePath, string suffix)
    {
        Directory.CreateDirectory(AppDataPaths.BackupsDirectory);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var path = Path.Combine(AppDataPaths.BackupsDirectory, $"{baseName}_{suffix}_{stamp}.json");

        var unique = UniquePath(path);
        File.Copy(sourcePath, unique, overwrite: false);
        return unique;
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var extension = Path.GetExtension(path);
        var baseName = Path.GetFileNameWithoutExtension(path);

        for (var i = 2; ; i++)
        {
            var candidate = Path.Combine(directory, $"{baseName} ({i}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}