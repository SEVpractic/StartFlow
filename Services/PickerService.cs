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
            new COMDLG_FILTERSPEC { pszName = "Программы", pszSpec = "*.exe;*.bat;*.cmd;*.lnk" },
            new COMDLG_FILTERSPEC { pszName = "Все файлы", pszSpec = "*.*" }
        };

        return Task.FromResult(CommonDialogs.PickFile(App.MainWindowHandle, RootCatalog, fileTypes));
    }

    private static string RootCatalog => Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
}