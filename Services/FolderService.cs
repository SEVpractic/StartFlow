using System.Diagnostics;
using System.Globalization;
using StartFlow.Models;

namespace StartFlow.Services;

public sealed class FolderService
{
    public static string ResolveTemplatePath(FolderGenerationConfig config)
    {
        var resolved = config.Template.Replace("{date}", DateTime.Today.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));

        if (string.IsNullOrWhiteSpace(resolved))
        {
            return config.Path;
        }

        if (IsPathRooted(resolved))
        {
            return resolved;
        }

        return Path.Combine(config.Path, resolved);
    }

    public bool EnsureFolderExists(string path)
    {
        Directory.CreateDirectory(path);
        return true;
    }

    public bool OpenInExplorer(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });

        return true;
    }

    private static bool IsPathRooted(string path)
    {
        try
        {
            return Path.IsPathRooted(path);
        }
        catch (Exception)
        {
            return path.Length >= 2 && path[1] == ':';
        }
    }
}