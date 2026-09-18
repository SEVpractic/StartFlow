using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace StartFlow.Services;

public static class PickerService
{
    public static async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder
        };
        picker.FileTypeFilter.Add("*");

        InitializeWithWindow.Initialize(picker, App.MainWindowHandle);

        StorageFolder? folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    public static async Task<string?> PickExecutableAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            CommitButtonText = "Выбрать"
        };
        picker.FileTypeFilter.Add(".exe");
        picker.FileTypeFilter.Add(".bat");
        picker.FileTypeFilter.Add(".cmd");
        picker.FileTypeFilter.Add(".lnk");

        InitializeWithWindow.Initialize(picker, App.MainWindowHandle);

        StorageFile? file = await picker.PickSingleFileAsync();
        return file?.Path;
    }
}