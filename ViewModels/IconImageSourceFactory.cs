using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace StartFlow.ViewModels;

/// <summary>
/// Превращает сырые пиксели иконки (BGRA) в ImageSource.
/// Метод должен вызываться из UI-потока.
/// </summary>
internal static class IconImageSourceFactory
{
    public static async Task<ImageSource?> CreateFromPixelDataAsync(byte[]? bgra)
    {
        if (bgra is null || bgra.Length == 0)
        {
            return null;
        }

        try
        {
            var size = (int)Math.Sqrt(bgra.Length / 4.0);
            if (size <= 0 || size * size * 4 != bgra.Length)
            {
                return null;
            }

            var writer = new DataWriter();
            writer.WriteBytes(bgra);
            var buffer = writer.DetachBuffer();

            var softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, size, size, BitmapAlphaMode.Premultiplied);
            softwareBitmap.CopyFromBuffer(buffer);

            var source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(softwareBitmap).AsTask();
            return source;
        }
        catch
        {
            return null;
        }
    }
}