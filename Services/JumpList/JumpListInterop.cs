using System.Runtime.InteropServices;
using System.Text;

namespace StartFlow.Services.JumpList;

/// <summary>
/// COM-интерфейсы Windows Shell, необходимые для постройки Jump List.
/// Идентификаторы соответствуют shobjidl_core.h и objectarray.h Windows SDK.
/// В атрибутах Guid литералы обязательны, поэтому они продублированы в
/// <see cref="JumpListInterop"/> — значения должны совпадать.
/// </summary>
[ComImport]
[Guid("6332DEBF-87B5-4670-90C0-5E57B408A49E")] // IID_ICustomDestinationList
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ICustomDestinationList
{
    void SetAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);

    void BeginList(out uint minSlots, ref Guid riid, out IntPtr destinationList);

    void AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string category, IObjectArray items);

    void AppendKnownCategory(int category);

    void AddUserTasks(IObjectArray tasks);

    void CommitList();

    void GetRemovedDestinations(ref Guid riid, out IntPtr removedDestinations);

    void DeleteList([MarshalAs(UnmanagedType.LPWStr)] string? appId);

    void AbortList();
}

/// <summary>
/// IPropertyStore применяется к IShellLink, чтобы задать отображаемое имя пункта
/// через PKEY_Title. Без него AppendCategory завершается E_INVALIDARG: имя показывается
/// именно из System.Title, а SetDescription даёт только всплывающую подсказку.
/// </summary>
[ComImport]
[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")] // IID_IPropertyStore
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    void GetCount(out uint count);

    void GetAt(uint index, out PropertyKey key);

    void GetValue(ref PropertyKey key, out PropVariant value);

    void SetValue(ref PropertyKey key, ref PropVariant value);

    void Commit();
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey
{
    public Guid FormatId;

    public uint PropertyId;
}

/// <summary>
/// Минимальная PROPVARIANT: используется только VT_LPWSTR, поэтому достаточно
/// типа и указателя на строку. Обходной путь в обход PropVariantInit — эта функция
/// не экспортируется ole32.dll, а объявления PROPVARIANT в .NET нет.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
internal struct PropVariant
{
    [FieldOffset(0)]
    public ushort VarType;

    [FieldOffset(8)]
    public IntPtr Pointer;
}

[ComImport]
[Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9")] // IID_IObjectArray
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IObjectArray
{
    void GetCount(out uint count);

    void GetAt(uint index, ref Guid riid, out IntPtr item);
}

/// <summary>
/// Наследует IObjectArray, как и в objectarray.h: AppendCategory принимает IObjectArray.
/// </summary>
[ComImport]
[Guid("5632B1A4-E38A-400A-928A-D4CD63230295")] // IID_IObjectCollection
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IObjectCollection : IObjectArray
{
    void GetObjectArray(out IObjectArray value);
}

[ComImport]
[Guid("000214F9-0000-0000-C000-000000000046")] // IID_IShellLinkW
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellLinkW
{
    void GetPath([MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, IntPtr findData, int flags);

    void GetIDList(out IntPtr idList);

    void SetIDList(IntPtr idList);

    void GetDescription([MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);

    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);

    void GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int maxDirectory);

    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);

    void GetArguments([MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int maxArguments);

    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);

    void GetHotkey(out ushort hotkey);

    void SetHotkey(ushort hotkey);

    void GetShowCmd(out int showCmd);

    void SetShowCmd(int showCmd);

    void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int iconPathMax, out int iconIndex);

    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);

    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, int reserved);

    void Resolve(IntPtr window, int flags);

    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
}

internal static class JumpListInterop
{
    public static readonly Guid ClsidDestinationList = new("77F10CF0-3DB5-4966-B520-B7C54FD35ED6");

    public static readonly Guid IidICustomDestinationList = new("6332DEBF-87B5-4670-90C0-5E57B408A49E");

    public static readonly Guid IidIObjectCollection = new("5632B1A4-E38A-400A-928A-D4CD63230295");

    public static readonly Guid IidIObjectArray = new("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9");

    public static readonly Guid IidIShellLinkW = new("000214F9-0000-0000-C000-000000000046");

    public static readonly Guid ClsidShellLink = new("00021401-0000-0000-C000-000000000046");

    public static readonly Guid IidIUnknown = new("00000000-0000-0000-C000-000000000046");

    public static readonly Guid IidIPropertyStore = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

    /// <summary>PKEY_Title — отображаемое имя пункта Jump List.</summary>
    public static readonly Guid PkeyTitle = new("F29F85E0-4FF9-1068-AB91-08002B27B3D9");

    public const uint PkeyTitleId = 2;

    public const ushort VarTypeLpWStr = 31;
}

internal static class ComInterop
{
    /// <summary>
    /// Создаёт COM-объект по CLSID. Shell-объекты возвращаются как System.__ComObject,
    /// поэтому приводить их нужно к интерфейсам, а не к типам coclass.
    /// </summary>
    public static object CoCreateObject(Guid clsid)
    {
        var type = Type.GetTypeFromCLSID(clsid, throwOnError: true)!;
        return Activator.CreateInstance(type)!;
    }

    public static T CoCreate<T>(Guid clsid) where T : class => (T)CoCreateObject(clsid);

    /// <summary>Получает указатель на дополнительный интерфейс существующего COM-объекта.</summary>
    public static IntPtr QueryInterface<TSource, TTarget>(TSource source)
        where TSource : class
        where TTarget : class
    {
        var unknown = Marshal.GetIUnknownForObject(source);
        try
        {
            var iid = typeof(TTarget).GUID;
            var hr = Marshal.QueryInterface(unknown, in iid, out var pointer);
            if (hr != 0 || pointer == IntPtr.Zero)
            {
                throw new COMException($"Объект не поддерживает интерфейс {iid}.", hr);
            }

            return pointer;
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    public static T InterfaceFromPointer<T>(IntPtr pointer) where T : class
        => (T)Marshal.GetObjectForIUnknown(pointer);
}