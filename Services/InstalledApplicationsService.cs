using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using StartFlow.Models;

namespace StartFlow.Services;

/// <summary>
/// Получение списка приложений, зарегистрированных в Windows
/// (список «Все приложения» / shell:AppsFolder).
///
/// UI не обращается к Windows Shell напрямую — только через этот сервис.
/// Механизм источника данных изолирован и может быть заменён без переписывания UI.
/// </summary>
public sealed partial class InstalledApplicationsService
{
    private const string AppsFolder = "shell:AppsFolder";
    private const string ClsidShellApplication = "13709620-C279-11CE-A49E-444553540000";

    private const string PropTargetParsingPath = "System.Link.TargetParsingPath";
    private const string PropLaunchArguments = "System.Link.Arguments";

    private const int IconSize = 32;

    private static readonly HashSet<string> LaunchableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".com", ".msc", ".ps1", ".vbs", ".lnk"
    };

    /// <summary>Получить список установленных приложений. Метод блокирующий; вызывать вне UI-потока.</summary>
    public IReadOnlyList<InstalledApplication> GetInstalledApplications()
    {
        var result = new List<InstalledApplication>();
        var items = TryGetItems();
        if (items is null)
        {
            return result;
        }

        try
        {
            var count = SafeCount(items);
            for (var i = 0; i < count; i++)
            {
                object? item;
                try
                {
                    item = items.Item(i);
                }
                catch
                {
                    continue;
                }

                if (item is null)
                {
                    continue;
                }

                var parsed = TryParse(item);
                if (parsed is not null)
                {
                    result.Add(parsed);
                }
            }
        }
        finally
        {
            TryRelease(items);
        }

        return result;
    }

    private dynamic? TryGetItems()
    {
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid(ClsidShellApplication));
            if (type is null)
            {
                return null;
            }

            dynamic? shell = Activator.CreateInstance(type);
            if (shell is null)
            {
                return null;
            }

            dynamic? ns = shell.Namespace(AppsFolder);
            return ns?.Items();
        }
        catch
        {
            return null;
        }
    }

    private static int SafeCount(dynamic items)
    {
        try
        {
            var value = (int)items.Count;
            return value < 0 ? 0 : value;
        }
        catch
        {
            return 0;
        }
    }

    private static string? GetStringProperty(dynamic item, string propertyName)
    {
        try
        {
            object? value = item.ExtendedProperty(propertyName);
            return value switch
            {
                null => null,
                string s => s,
                _ => value.ToString()
            };
        }
        catch
        {
            return null;
        }
    }

    private static InstalledApplication? TryParse(dynamic item)
    {
        string name;
        try
        {
            name = (string)item.Name;
        }
        catch
        {
            return null;
        }

        name = name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return null;
        }

        var appUserModelId = GetPathProperty(item);
        var target = GetStringProperty(item, PropTargetParsingPath);
        var arguments = GetStringProperty(item, PropLaunchArguments);

        return Classify(name, appUserModelId, target, arguments);
    }

    /// <summary>Pseudo-путь элемента (AUMID для приложений Store/MSIX, идентификатор для остальных).</summary>
    private static string? GetPathProperty(dynamic item)
    {
        try
        {
            return item.Path as string;
        }
        catch
        {
            return null;
        }
    }

    private static InstalledApplication? Classify(string name, string? appUserModelId, string? target, string? arguments)
    {
        if (IsPackagedApp(appUserModelId, target))
        {
            return new InstalledApplication
            {
                Name = name,
                AppUserModelId = appUserModelId,
                LaunchArguments = arguments,
                CanAddAsProgram = false,
                AddRestrictionReason = "Приложение Store/MSIX: запускается только через Windows, пути к exe нет"
            };
        }

        target ??= appUserModelId;

        if (IsNamespaceItem(target))
        {
            return null;
        }

        if (IsExternalLocation(target))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(target) || !LaunchableExtensions.Contains(Path.GetExtension(target)))
        {
            return null;
        }

        var exists = false;
        try
        {
            exists = File.Exists(target);
        }
        catch
        {
            exists = false;
        }

        if (!exists)
        {
            return null!;
        }

        return new InstalledApplication
        {
            Name = name,
            AppUserModelId = appUserModelId,
            ExecutablePath = target,
            LaunchArguments = arguments,
            CanAddAsProgram = true,
            IconData = TryExtractIcon(target)
        };
    }

    private static bool IsPackagedApp(string? appUserModelId, string? target)
    {
        if (!string.IsNullOrWhiteSpace(target))
        {
            var normal = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var windowsApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
            if (normal.StartsWith(Path.GetFullPath(windowsApps), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return !string.IsNullOrWhiteSpace(appUserModelId) && PackagedAumidRegex().IsMatch(appUserModelId);
    }

    private static bool IsNamespaceItem(string? target)
        => !string.IsNullOrWhiteSpace(target) && target.StartsWith("::", StringComparison.Ordinal);

    /// <summary>Ссылки на сайты/документы, а не на запускаемые приложения.</summary>
    private static bool IsExternalLocation(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        if (target.Contains("://", StringComparison.Ordinal))
        {
            return true;
        }

        var extension = Path.GetExtension(target);
        return extension.Length > 0
            && !LaunchableExtensions.Contains(extension)
            && !string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static byte[]? TryExtractIcon(string path)
    {
        var shFileInfo = default(SHFILEINFO);
        var result = SHGetFileInfo(path, 0, ref shFileInfo, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_SHELLICONSIZE);
        var hIcon = shFileInfo.hIcon;
        if (result == IntPtr.Zero || hIcon == IntPtr.Zero)
        {
            return null;
        }

        var screenDc = IntPtr.Zero;
        var memDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        try
        {
            screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero)
            {
                return null;
            }

            memDc = CreateCompatibleDC(screenDc);
            bitmap = CreateCompatibleBitmap(screenDc, IconSize, IconSize);
            if (memDc == IntPtr.Zero || bitmap == IntPtr.Zero)
            {
                return null;
            }

            var previous = SelectObject(memDc, bitmap);
            DrawIconEx(memDc, 0, 0, hIcon, IconSize, IconSize, 0, IntPtr.Zero, DI_NORMAL);
            SelectObject(memDc, previous);

            var header = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = IconSize,
                biHeight = -IconSize,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0
            };

            var bytes = new byte[IconSize * IconSize * 4];
            var lines = GetDIBits(memDc, bitmap, 0, (uint)IconSize, bytes, ref header, DIB_RGB_COLORS);
            return lines == 0 ? null : bytes;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (memDc != IntPtr.Zero && bitmap != IntPtr.Zero)
            {
                DeleteObject(bitmap);
            }

            if (memDc != IntPtr.Zero)
            {
                DeleteDC(memDc);
            }

            if (screenDc != IntPtr.Zero)
            {
                ReleaseDC(IntPtr.Zero, screenDc);
            }

            DestroyIcon(hIcon);
        }
    }

    private static void TryRelease(dynamic comObject)
    {
        try
        {
            Marshal.ReleaseComObject(comObject);
        }
        catch
        {
            // RCW мог быть освобождён сборщиком мусора — это допустимо.
        }
    }

    [GeneratedRegex(@"^[^\\/]+_[a-zA-Z0-9]{13}![^\s]+$", RegexOptions.Compiled)]
    private static partial Regex PackagedAumidRegex();

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_SHELLICONSIZE = 0x000000004;
    private const uint DI_NORMAL = 0x0003;
    private const uint DIB_RGB_COLORS = 0x0000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr hIcon, int cx, int cy, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, byte[] lpvBits, ref BITMAPINFOHEADER lpbmi, uint uUsage);
}