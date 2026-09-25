using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace StartFlow.ViewModels;

public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

public sealed class NotNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is not null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// Показывает сырые пиксели иконки (BGRA) как ImageSource для списка приложений.
/// Использует кэш — одни и те же пиксели не пересоздаются при повторной фильтрации.
/// </summary>
public sealed class IconBytesToImageConverter : IValueConverter
{
    private const int IconSize = 32;

    private static readonly Dictionary<byte[], WriteableBitmap> Cache = new();

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not byte[] bytes || bytes.Length != IconSize * IconSize * 4)
        {
            return null;
        }

        if (Cache.TryGetValue(bytes, out var cached))
        {
            return cached;
        }

        var bitmap = new WriteableBitmap(IconSize, IconSize);
        var bufferAccess = (IBufferByteAccess)bitmap.PixelBuffer;
        bufferAccess.Buffer(out var pointer);
        Marshal.Copy(bytes, 0, pointer, bytes.Length);

        Cache[bytes] = bitmap;
        return bitmap;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    [ComImport]
    [Guid("905a0fef-bc53-11df-8c49-001e4fc686da")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IBufferByteAccess
    {
        [PreserveSig]
        int Buffer(out IntPtr value);
    }
}