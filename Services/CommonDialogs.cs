using System.Runtime.InteropServices;

namespace StartFlow.Services;

internal static class CommonDialogs
{
    private const int HRESULT_OK = 0;
    private const uint FOS_OVERWRITEPROMPT = 0x00000002;
    private const uint FOS_PICKFOLDERS = 0x00000020;
    private const uint FOS_FORCEFILESYSTEM = 0x00000040;
    private const uint FOS_FILEMUSTEXIST = 0x00001000;
    private const uint SIGDN_FILESYSPATH = 0x80058000;

    public static string? PickFolder(IntPtr owner, string startPath)
        => PickCore(owner, startPath, pickFolders: true, fileTypes: null);

    public static string? PickFile(IntPtr owner, string startPath, COMDLG_FILTERSPEC[] fileTypes)
        => PickCore(owner, startPath, pickFolders: false, fileTypes: fileTypes);

    public static string? PickSaveFile(IntPtr owner, string startPath, string suggestedFileName, COMDLG_FILTERSPEC[] fileTypes)
    {
        var dialog = (IFileSaveDialog)new FileSaveDialogRCW();
        try
        {
            dialog.SetOptions(FOS_FORCEFILESYSTEM | FOS_OVERWRITEPROMPT);

            if (fileTypes is { Length: > 0 })
            {
                dialog.SetFileTypes((uint)fileTypes.Length, fileTypes);
            }

            if (!string.IsNullOrWhiteSpace(suggestedFileName))
            {
                dialog.SetFileName(suggestedFileName);
                dialog.SetDefaultExtension("json");
            }

            SetStartFolder(dialog, startPath);

            if (dialog.Show(owner) != HRESULT_OK)
            {
                return null;
            }

            IShellItem? result;
            if (dialog.GetResult(out result) != HRESULT_OK || result is null)
            {
                return null;
            }

            return GetPath(result);
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    private static string? PickCore(IntPtr owner, string startPath, bool pickFolders, COMDLG_FILTERSPEC[]? fileTypes)
    {
        var dialog = (IFileOpenDialog)new FileOpenDialogRCW();
        try
        {
            uint options = FOS_FORCEFILESYSTEM | (pickFolders ? FOS_PICKFOLDERS : FOS_FILEMUSTEXIST);
            dialog.SetOptions(options);

            if (fileTypes is { Length: > 0 })
            {
                dialog.SetFileTypes((uint)fileTypes.Length, fileTypes);
            }

            SetStartFolder(dialog, startPath);

            if (dialog.Show(owner) != HRESULT_OK)
            {
                return null;
            }

            IShellItem? result;
            if (dialog.GetResult(out result) != HRESULT_OK || result is null)
            {
                return null;
            }

            return GetPath(result);
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    private static void SetStartFolder(IFileOpenDialog dialog, string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            return;
        }

        var riid = typeof(IShellItem).GUID;
        IShellItem? item;
        if (SHCreateItemFromParsingName(startPath, IntPtr.Zero, ref riid, out item) == HRESULT_OK && item is not null)
        {
            try
            {
                dialog.SetFolder(item);
            }
            finally
            {
                Marshal.ReleaseComObject(item);
            }
        }
    }

    private static void SetStartFolder(IFileSaveDialog dialog, string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            return;
        }

        var riid = typeof(IShellItem).GUID;
        IShellItem? item;
        if (SHCreateItemFromParsingName(startPath, IntPtr.Zero, ref riid, out item) == HRESULT_OK && item is not null)
        {
            try
            {
                dialog.SetFolder(item);
            }
            finally
            {
                Marshal.ReleaseComObject(item);
            }
        }
    }

    private static string? GetPath(IShellItem item)
    {
        IntPtr pointer;
        if (item.GetDisplayName(SIGDN_FILESYSPATH, out pointer) != HRESULT_OK || pointer == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUni(pointer);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [In] ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);
}

[ComImport]
[Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
internal class FileOpenDialogRCW
{
}

[ComImport]
[Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B")]
internal class FileSaveDialogRCW
{
}

[ComImport]
[Guid("84BCCD23-5FDE-4CDB-AEA4-AF64B83D78AB")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileSaveDialog
{
    // IModalWindow
    [PreserveSig]
    int Show(IntPtr hwndOwner);

    // IFileDialog
    [PreserveSig]
    int SetFileTypes(uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] COMDLG_FILTERSPEC[] rgFilterSpec);

    [PreserveSig]
    int SetFileTypeIndex(uint iFileType);

    [PreserveSig]
    int GetFileTypeIndex(out uint piFileType);

    [PreserveSig]
    int Advise(IntPtr pfde, out uint pdwCookie);

    [PreserveSig]
    int Unadvise(uint dwCookie);

    [PreserveSig]
    int SetOptions(uint fos);

    [PreserveSig]
    int GetOptions(out uint pfos);

    [PreserveSig]
    int SetDefaultFolder(IShellItem psi);

    [PreserveSig]
    int SetFolder(IShellItem psi);

    [PreserveSig]
    int GetFolder(out IShellItem ppsi);

    [PreserveSig]
    int GetCurrentSelection(out IShellItem ppsi);

    [PreserveSig]
    int SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);

    [PreserveSig]
    int GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszFilename);

    [PreserveSig]
    int SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);

    [PreserveSig]
    int SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);

    [PreserveSig]
    int SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

    [PreserveSig]
    int GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

    [PreserveSig]
    int AddPlace(IShellItem psi, int fdap);

    [PreserveSig]
    int SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszExt);

    [PreserveSig]
    int Close(int hr);

    [PreserveSig]
    int SetClientGuid(ref Guid guid);

    [PreserveSig]
    int ClearClientData();

    [PreserveSig]
    int SetFilter(IntPtr pFilter);

    // IFileSaveDialog
    [PreserveSig]
    int SetSaveAsItem(IShellItem psi);

    [PreserveSig]
    int SetProperties(IntPtr pStore);

    [PreserveSig]
    int SetCollectedProperties(IntPtr pList);

    [PreserveSig]
    int GetProperties(out IntPtr pStore);

    [PreserveSig]
    int ApplyProperties(IShellItem psi);
}

[ComImport]
[Guid("D57C7288-D4AD-4768-BE02-9D969532D960")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOpenDialog
{
    // IModalWindow
    [PreserveSig]
    int Show(IntPtr hwndOwner);

    // IFileDialog
    [PreserveSig]
    int SetFileTypes(uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] COMDLG_FILTERSPEC[] rgFilterSpec);

    [PreserveSig]
    int SetFileTypeIndex(uint iFileType);

    [PreserveSig]
    int GetFileTypeIndex(out uint piFileType);

    [PreserveSig]
    int Advise(IntPtr pfde, out uint pdwCookie);

    [PreserveSig]
    int Unadvise(uint dwCookie);

    [PreserveSig]
    int SetOptions(uint fos);

    [PreserveSig]
    int GetOptions(out uint pfos);

    [PreserveSig]
    int SetDefaultFolder(IShellItem psi);

    [PreserveSig]
    int SetFolder(IShellItem psi);

    [PreserveSig]
    int GetFolder(out IShellItem ppsi);

    [PreserveSig]
    int GetCurrentSelection(out IShellItem ppsi);

    [PreserveSig]
    int SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);

    [PreserveSig]
    int GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszFilename);

    [PreserveSig]
    int SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);

    [PreserveSig]
    int SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);

    [PreserveSig]
    int SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

    [PreserveSig]
    int GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

    [PreserveSig]
    int AddPlace(IShellItem psi, int fdap);

    [PreserveSig]
    int SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszExt);

    [PreserveSig]
    int Close(int hr);

    [PreserveSig]
    int SetClientGuid(ref Guid guid);

    [PreserveSig]
    int ClearClientData();

    [PreserveSig]
    int SetFilter(IntPtr pFilter);

    // IFileOpenDialog
    [PreserveSig]
    int GetResults(out IntPtr ppenum);

    [PreserveSig]
    int GetSelectedItems(out IntPtr ppsai);
}

[ComImport]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    [PreserveSig]
    int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);

    [PreserveSig]
    int GetParent(out IShellItem ppsi);

    [PreserveSig]
    int GetDisplayName(uint sigdnName, out IntPtr ppszName);

    [PreserveSig]
    int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

    [PreserveSig]
    int Compare(IShellItem psi, uint hint, out int piOrder);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct COMDLG_FILTERSPEC
{
    [MarshalAs(UnmanagedType.LPWStr)]
    public string pszName;

    [MarshalAs(UnmanagedType.LPWStr)]
    public string pszSpec;
}