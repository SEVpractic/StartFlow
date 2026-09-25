using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using StartFlow.Services;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace StartFlow.ViewModels;

/// <summary>
/// Превращает сырые пиксели иконки (BGRA) в ImageSource, а также извлекает
/// иконку по пути к файлу. Методы должны вызываться из UI-потока.
/// </summary>
internal static class IconImageSourceFactory
{
    public static async Task<ImageSource?> CreateFromPathAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var bytes = await Task.Run(() => InstalledApplicationsService.ExtractIconBytes(path));
        return await CreateFromPixelDataAsync(bytes);
    }

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

            PremultiplyAlpha(bgra);

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

    private static void PremultiplyAlpha(byte[] bgra)
    {
        for (var i = 0; i < bgra.Length; i += 4)
        {
            var a = bgra[i + 3];
            if (a == 255)
            {
                continue;
            }

            if (a == 0)
            {
                bgra[i] = 0;
                bgra[i + 1] = 0;
                bgra[i + 2] = 0;
                continue;
            }

            bgra[i] = (byte)(bgra[i] * a / 255);
            bgra[i + 1] = (byte)(bgra[i + 1] * a / 255);
            bgra[i + 2] = (byte)(bgra[i + 2] * a / 255);
        }
    }
}