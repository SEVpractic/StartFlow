using System.IO;

namespace StartFlow.Services;

public static class PickerService
{
    public static Task<string?> PickFolderAsync()
        => Task.FromResult(CommonDialogs.PickFolder(App.MainWindowHandle, RootCatalog));

    public static Task<string?> PickExecutableAsync()
    {
        var fileTypes = new[]
        {
            new COMDLG_FILTERSPEC { pszName = "Исполняемые файлы (*.exe)", pszSpec = "*.exe" },
            new COMDLG_FILTERSPEC { pszName = "Командные скрипты (*.cmd;*.bat)", pszSpec = "*.cmd;*.bat" },
            new COMDLG_FILTERSPEC { pszName = "Все поддерживаемые файлы (*.exe;*.cmd;*.bat)", pszSpec = "*.exe;*.cmd;*.bat" },
            new COMDLG_FILTERSPEC { pszName = "Все файлы", pszSpec = "*.*" }
        };

        return Task.FromResult(CommonDialogs.PickFile(App.MainWindowHandle, RootCatalog, fileTypes));
    }

    public static Task<string?> PickProfileFileAsync()
    {
        var fileTypes = new[]
        {
            new COMDLG_FILTERSPEC { pszName = "Профили StartFlow", pszSpec = "*.json" },
            new COMDLG_FILTERSPEC { pszName = "Все файлы", pszSpec = "*.*" }
        };

        return Task.FromResult(CommonDialogs.PickFile(App.MainWindowHandle, RootCatalog, fileTypes));
    }

    public static Task<string?> PickSaveProfileAsync(string suggestedFileName)
    {
        var fileTypes = new[]
        {
            new COMDLG_FILTERSPEC { pszName = "Профили StartFlow", pszSpec = "*.json" },
            new COMDLG_FILTERSPEC { pszName = "Все файлы", pszSpec = "*.*" }
        };

        return Task.FromResult(CommonDialogs.PickSaveFile(App.MainWindowHandle, RootCatalog, suggestedFileName, fileTypes));
    }

    private static string RootCatalog => Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
}