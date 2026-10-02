using System.Globalization;
using System.Runtime.InteropServices;
using StartFlow.Services.JumpList;

namespace StartFlow.Services;

/// <summary>
/// Интеграция StartFlow с Windows Jump List.
///
/// Приложение остаётся unpackaged (без package identity), поэтому используется штатный
/// Win32 Shell API — ICustomDestinationList (CLSID_DestinationList, объявление в
/// shobjidl_core.h). Список строится как custom-категория «Тихий запуск» с одним
/// пунктом на каждый существующий профиль. Пункт запускает уже имеющийся сценарий
/// StartFlow: --silent --profile "Имя".
///
/// Интеграция дополнительная: любые ошибки Windows API проглатываются и попадают в
/// каталог logs, не влияя на запуск, профили и интерфейс приложения.
///
/// Работа выполняется в apartment вызывающего потока. В GUI это STA основного потока,
/// в silent-режиме — MTA, который живёт до конца процесса. Временные потоки не
/// используются намеренно: COM-контейнер, создающий объекты Jump List, не должен
/// завершаться раньше, чем Shell перестанет их использовать.
/// </summary>
public sealed class JumpListService
{
    /// <summary>
    /// Постоянный AppUserModelID приложения. Не зависит от версии StartFlow, пути
    /// публикации, имени пользователя и имён профилей.
    /// </summary>
    public const string AppUserModelId = "StartFlow.StartFlow";

    /// <summary>Заголовок custom-категории Jump List.</summary>
    public const string SilentRunCategoryName = "Тихий запуск";

    private const string LogFileName = "jumplist.log";

    private static readonly object SyncRoot = new();
    private static readonly object LogSyncRoot = new();
    private static bool _appUserModelIdApplied;
    private static JumpListService? _default;

    private readonly ConfigurationService _configuration;

    public JumpListService(ConfigurationService configuration)
    {
        _configuration = configuration;
    }

    /// <summary>Экземпляр, работающий с основной конфигурацией приложения.</summary>
    public static JumpListService Default => _default ??= new JumpListService(App.Configuration);

    /// <summary>
    /// Задаёт процессу постоянный AppUserModelID. Вызывается один раз, до любой работы
    /// с Jump List и до создания окна.
    /// </summary>
    public static void EnsureAppUserModelId()
    {
        if (_appUserModelIdApplied)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_appUserModelIdApplied)
            {
                return;
            }

            try
            {
                var hr = NativeMethods.SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
                if (hr < 0)
                {
                    WriteLog($"Не удалось задать AppUserModelID «{AppUserModelId}»: HRESULT 0x{hr:X8}");
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                WriteLog($"SetCurrentProcessExplicitAppUserModelID недоступен: {ex.Message}");
            }

            _appUserModelIdApplied = true;
        }
    }

    /// <summary>
    /// Приводит Jump List в соответствие текущему списку профилей.
    /// Не выбрасывает наружу исключений: сбой интеграции не должен влиять на StartFlow.
    /// </summary>
    public void Refresh()
    {
        try
        {
            EnsureAppUserModelId();

            lock (SyncRoot)
            {
                var names = _configuration.Profiles.ListProfileNames();
                if (names.Count == 0)
                {
                    WriteLog("Список профилей пуст — Jump List не изменялась.");
                    return;
                }

                Commit(names, GetExecutablePath());
            }
        }
        catch (Exception ex)
        {
            WriteLog("Не удалось обновить Jump List: " + ex.Message);
        }
    }

    /// <summary>
    /// Заменяет содержимое Jump List одной custom-категорией, по пункту на профиль.
    /// Ограничения на число профилей нет: Windows сама решает, сколько пунктов
    /// помещается в меню, и показывает остальные по прокрутке.
    /// </summary>
    private static void Commit(IReadOnlyList<string> names, string executablePath)
    {
        ICustomDestinationList? list = null;
        var listStarted = false;
        var committed = false;

        try
        {
            var links = BuildLinks(names, executablePath);
            if (links.Count == 0)
            {
                WriteLog("Не удалось создать ни одного пункта Jump List — список не изменялся.");
                return;
            }

            list = ComInterop.CoCreate<ICustomDestinationList>(JumpListInterop.ClsidDestinationList);

            // Явно повторяет AppUserModelID процесса: список должен писаться
            // в ветку CustomDestinations, отведённую именно StartFlow.
            list.SetAppID(AppUserModelId);

            // ppv запрашивается как IUnknown: возвращённый объект нужен только на время
            // AppendCategory/CommitList, а IID_IDestinationList в доступных заголовках
            // SDK отсутствует и различается между версиями Windows.
            var riid = JumpListInterop.IidIUnknown;
            list.BeginList(out var minSlots, ref riid, out _);
            listStarted = true;

            var categoryAdded = true;
            try
            {
                list.AppendCategory(SilentRunCategoryName, new ShellObjectCollection(links.Cast<object>()));
            }
            catch (UnauthorizedAccessException)
            {
                // Задокументированное поведение: пользовательская настройка приватности
                // «Показывать недавние элементы в списках переходов» запрещает
                // пользовательские категории. Список при этом нужно всё равно зафиксировать,
                // чтобы Shell сбросил список удалённых пунктов.
                categoryAdded = false;
                WriteLog(
                    "Windows запретил пользовательскую категорию Jump List (E_ACCESSDENIED). " +
                    "Включите Параметры → Конфиденциальность и безопасность → " +
                    "«Показывать недавние элементы в списках переходов». " +
                    "Пункты запускаются из меню «Пуск» и по ярлыку.");
            }

            list.CommitList();
            committed = true;

            WriteLog(categoryAdded
                ? $"Jump List обновлён: категория «{SilentRunCategoryName}», пунктов {links.Count}, " +
                  $"слотов до вызова {minSlots}."
                : $"Jump List зафиксирован без пользовательской категории: пунктов {links.Count}, " +
                  $"слотов до вызова {minSlots}.");
        }
        catch (Exception ex)
        {
            WriteLog($"Не удалось построить Jump List [{ex.GetType().Name}]: {ex}");
        }
        finally
        {
            if (listStarted && !committed)
            {
                try
                {
                    list?.AbortList();
                }
                catch (Exception ex)
                {
                    WriteLog("AbortList завершился ошибкой: " + ex.Message);
                }
            }
        }
    }

    private static List<IShellLinkW> BuildLinks(IReadOnlyList<string> names, string executablePath)
    {
        var links = new List<IShellLinkW>(names.Count);

        foreach (var name in names)
        {
            var link = CreateLink(executablePath, name);
            if (link is not null)
            {
                links.Add(link);
            }
        }

        return links;
    }

    private static IShellLinkW? CreateLink(string executablePath, string profileName)
    {
        try
        {
            var link = ComInterop.CoCreate<IShellLinkW>(JumpListInterop.ClsidShellLink);
            link.SetPath(executablePath);
            link.SetWorkingDirectory(Path.GetDirectoryName(executablePath) ?? executablePath);

            // Обязательная тройка по документации AppendCategory: путь, аргументы, иконка.
            link.SetArguments(BuildSilentArguments(profileName));
            link.SetIconLocation(executablePath, 0);

            // SetDescription даёт только подсказку; имя пункта берётся из PKEY_Title.
            link.SetDescription(profileName);
            SetTitle(link, profileName);

            return link;
        }
        catch (Exception ex)
        {
            WriteLog($"Не удалось создать пункт Jump List для профиля «{profileName}»: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Задаёт отображаемое имя пункта через System.Title (PKEY_Title).
    /// Без этого Shell отклоняет категорию с E_INVALIDARG.
    /// </summary>
    private static void SetTitle(IShellLinkW link, string title)
    {
        var pointer = IntPtr.Zero;
        var value = default(PropVariant);

        try
        {
            pointer = ComInterop.QueryInterface<IShellLinkW, IPropertyStore>(link);
            var store = ComInterop.InterfaceFromPointer<IPropertyStore>(pointer);

            var key = new PropertyKey
            {
                FormatId = JumpListInterop.PkeyTitle,
                PropertyId = JumpListInterop.PkeyTitleId
            };

            value.VarType = JumpListInterop.VarTypeLpWStr;
            value.Pointer = Marshal.StringToCoTaskMemUni(title);

            store.SetValue(ref key, ref value);
            store.Commit();
        }
        catch (Exception ex)
        {
            WriteLog($"Не удалось задать имя пункта Jump List «{title}»: {ex.Message}");
        }
        finally
        {
            if (value.Pointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(value.Pointer);
            }

            if (pointer != IntPtr.Zero)
            {
                Marshal.Release(pointer);
            }
        }
    }

    /// <summary>
    /// Аргументы командной строки для пункта Jump List. Имя профиля заключается в
    /// кавычки, чтобы корректно передавать профили с пробелами.
    /// </summary>
    public static string BuildSilentArguments(string profileName)
        => $"--silent --profile \"{profileName.Replace("\"", string.Empty)}\"";

    private static string GetExecutablePath()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return System.Reflection.Assembly.GetEntryAssembly()?.Location
               ?? Path.Combine(AppContext.BaseDirectory, "StartFlow.exe");
    }

    private static void WriteLog(string message)
    {
        try
        {
            Directory.CreateDirectory(AppDataPaths.LogsDirectory);
            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            var path = Path.Combine(AppDataPaths.LogsDirectory, LogFileName);

            lock (LogSyncRoot)
            {
                File.AppendAllText(path, line);
            }
        }
        catch
        {
            // Логирование — best effort.
        }
    }

    private static class NativeMethods
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    }
}