using Microsoft.UI.Xaml;
using StartFlow.Models;
using StartFlow.Services;

namespace StartFlow;

public partial class App : Application
{
    private const string SilentSwitch = "--silent";
    private const string HelpSwitch = "--help";
    private const string ShortHelpSwitch = "-h";
    private const string SlashHelpSwitch = "/?";
    private const string ProfileSwitch = "--profile";

    public static ConfigurationService Configuration { get; } = new();

    public static MainWindow? MainWindowInstance { get; internal set; }

    public static nint MainWindowHandle { get; internal set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // AppUserModelID задаётся до создания окна и до работы с Jump List.
        JumpListService.EnsureAppUserModelId();

        var arguments = ParseArguments(Environment.GetCommandLineArgs().Skip(1));

        if (arguments.ShowHelp)
        {
            ConsoleHelper.Print(HelpText);
            Environment.Exit(0);
            return;
        }

        // Jump List должна быть актуальной до того, как пользователь откроет меню.
        RefreshJumpList();

        if (arguments.Silent)
        {
            RunSilent(arguments.ProfileName);
            return;
        }

        MainWindowInstance = new MainWindow();
        MainWindowInstance.Activate();
    }

    /// <summary>
    /// Приводит Jump List в актуальное состояние. Вызывается при запуске, а также
    /// после изменения состава профилей.
    /// </summary>
    public static void RefreshJumpList() => JumpListService.Default.Refresh();

    private static void RunSilent(string? profileName)
    {
        var exitCode = 0;

        try
        {
            StartFlowConfig config;
            string profileLabel;

            if (string.IsNullOrWhiteSpace(profileName))
            {
                config = Configuration.Load();
                profileLabel = Configuration.ActiveProfileName;
            }
            else
            {
                var profile = Configuration.Profiles.LoadProfileForRead(profileName, out var profileWarning);
                config = profile.Config;
                profileLabel = profile.Name;
                PrintWarning(profileWarning);
            }

            PrintWarning(Configuration.LoadWarning);

            var startupService = new StartupService(Configuration);
            var result = startupService.RunAsync(config).GetAwaiter().GetResult();

            var lines = new List<string>
            {
                $"Профиль: {profileLabel}",
                "StartFlow — подготовка рабочего окружения.",
                $"Создано папок: {result.CreatedFolders}",
                $"Открыто папок: {result.OpenedFolders}",
                $"Запущено приложений: {result.StartedPrograms}",
                $"Пропущено: {result.SkippedPrograms}",
                $"Ошибок: {result.Errors.Count}"
            };

            foreach (var error in result.Errors)
            {
                lines.Add(error.ToString());
            }

            ConsoleHelper.Print(string.Join(Environment.NewLine, lines));

            exitCode = result.HasErrors ? 1 : 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.Print($"StartFlow: ошибка при выполнении: {ex.Message}");
            exitCode = 2;
        }

        Environment.Exit(exitCode);
    }

    private static void PrintWarning(string? warning)
    {
        if (!string.IsNullOrWhiteSpace(warning))
        {
            ConsoleHelper.Print($"Предупреждение: {warning}");
        }
    }

    private sealed class ParsedArguments
    {
        public bool Silent { get; init; }

        public bool ShowHelp { get; init; }

        /// <summary>Логическое имя профиля, заданное через --profile.</summary>
        public string? ProfileName { get; init; }
    }

    /// <summary>
    /// Разбирает аргументы командной строки. Значения сохраняются как есть,
    /// чтобы имена профилей не зависели от регистра и содержали пробелы.
    /// </summary>
    private static ParsedArguments ParseArguments(IEnumerable<string> args)
    {
        var silent = false;
        var showHelp = false;
        string? profile = null;

        using var enumerator = args.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var current = enumerator.Current.Trim();
            if (current.Length == 0)
            {
                continue;
            }

            if (IsSwitch(current, SilentSwitch))
            {
                silent = true;
                continue;
            }

            if (IsSwitch(current, HelpSwitch) || IsSwitch(current, ShortHelpSwitch) || IsSwitch(current, SlashHelpSwitch))
            {
                showHelp = true;
                continue;
            }

            if (IsSwitch(current, ProfileSwitch))
            {
                if (enumerator.MoveNext())
                {
                    var value = enumerator.Current.Trim().Trim('"');
                    if (value.Length > 0)
                    {
                        profile = value;
                    }
                }

                continue;
            }

            if (current.StartsWith(ProfileSwitch + "=", StringComparison.OrdinalIgnoreCase))
            {
                var value = current[(ProfileSwitch.Length + 1)..].Trim().Trim('"');
                if (value.Length > 0)
                {
                    profile = value;
                }
            }
        }

        return new ParsedArguments { Silent = silent, ShowHelp = showHelp, ProfileName = profile };
    }

    private static bool IsSwitch(string value, string expected)
        => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private static readonly string HelpText =
        "StartFlow — подготовка рабочего окружения при запуске." + Environment.NewLine +
        Environment.NewLine +
        "Использование:" + Environment.NewLine +
        "  StartFlow.exe                    Открыть графический интерфейс." + Environment.NewLine +
        "  StartFlow.exe --silent           Выполнить активный профиль без окна и завершиться." + Environment.NewLine +
        "  StartFlow.exe --silent --profile \"Имя\"   Выполнить указанный профиль без окна." + Environment.NewLine +
        "  StartFlow.exe --help             Показать эту справку." + Environment.NewLine +
        Environment.NewLine +
        "Пользовательские данные (профили, резервные копии, состояние) хранятся в:" + Environment.NewLine +
        "  %LOCALAPPDATA%\\StartFlow\\" + Environment.NewLine +
        Environment.NewLine +
        "Активный профиль можно переключить в настройках приложения." +
        Environment.NewLine +
        Environment.NewLine +
        "Тихий запуск отдельных профилей доступен также через Jump List значка StartFlow " +
        "на панели задач.";
}