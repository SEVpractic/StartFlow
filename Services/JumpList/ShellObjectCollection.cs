using System.Runtime.InteropServices;

namespace StartFlow.Services.JumpList;

/// <summary>
/// Реализация IObjectCollection поверх обычного списка COM-объектов.
/// Нужна, чтобы передать набор IShellLink в ICustomDestinationList.
/// </summary>
[ComVisible(true)]
[Guid("A1B2C3D4-1111-4444-8888-9F0E1D2C3B4A")]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class ShellObjectCollection : IObjectCollection
{
    private readonly List<object> _items;

    public ShellObjectCollection(IEnumerable<object> items)
    {
        _items = new List<object>(items);
    }

    public void GetCount(out uint count)
    {
        count = (uint)_items.Count;
    }

    public void GetAt(uint index, ref Guid riid, out IntPtr item)
    {
        if (index >= (uint)_items.Count)
        {
            item = IntPtr.Zero;
            Marshal.ThrowExceptionForHR(CorError.E_BOUNDS);
            return;
        }

        var pointer = IntPtr.Zero;
        try
        {
            pointer = Marshal.GetIUnknownForObject(_items[(int)index]);

            // Shell запрашивает здесь IID_IUnknown, но содержимое категории он принимает
            // только как IShellLink: возврат IUnknown даёт E_INVALIDARG.
            var shellLinkIid = JumpListInterop.IidIShellLinkW;
            var hr = Marshal.QueryInterface(pointer, in shellLinkIid, out item);
            if (hr != 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }
        finally
        {
            if (pointer != IntPtr.Zero)
            {
                Marshal.Release(pointer);
            }
        }
    }

    public void GetObjectArray(out IObjectArray value)
    {
        value = new ShellObjectArray(_items);
    }
}

[ComVisible(true)]
[Guid("A1B2C3D4-2222-4444-8888-9F0E1D2C3B4A")]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class ShellObjectArray : IObjectArray
{
    private readonly List<object> _items;

    public ShellObjectArray(IEnumerable<object> items)
    {
        _items = new List<object>(items);
    }

    public void GetCount(out uint count)
    {
        count = (uint)_items.Count;
    }

    public void GetAt(uint index, ref Guid riid, out IntPtr item)
    {
        if (index >= (uint)_items.Count)
        {
            item = IntPtr.Zero;
            Marshal.ThrowExceptionForHR(CorError.E_BOUNDS);
            return;
        }

        var pointer = IntPtr.Zero;
        try
        {
            pointer = Marshal.GetIUnknownForObject(_items[(int)index]);

            var shellLinkIid = JumpListInterop.IidIShellLinkW;
            var hr = Marshal.QueryInterface(pointer, in shellLinkIid, out item);
            if (hr != 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }
        finally
        {
            if (pointer != IntPtr.Zero)
            {
                Marshal.Release(pointer);
            }
        }
    }
}

internal static class CorError
{
    public const int E_BOUNDS = unchecked((int)0x8000000B);
}