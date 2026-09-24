using Microsoft.UI.Xaml;
using StartFlow.Services;

namespace StartFlow;

public partial class App : Application
{
    public static ConfigurationService Configuration { get; } = new();

    public static MainWindow? MainWindowInstance { get; internal set; }

    public static nint MainWindowHandle { get; internal set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var arguments = Environment.GetCommandLineArgs()
            .Skip(1)
            .Select(a => a.Trim().ToLowerInvariant())
            .ToList();

        if (arguments.Contains("--help") || arguments.Contains("-h") || arguments.Contains("/?"))
        {
            ConsoleHelper.Print(HelpText);
            Environment.Exit(0);
            return;
        }

        if (arguments.Contains("--silent"))
        {
            RunSilent();
            return;
        }

        MainWindowInstance = new MainWindow();
        MainWindowInstance.Activate();
    }

    private static void RunSilent()
    {
        var exitCode = 0;

        try
        {
            var config = Configuration.Load();

            if (!string.IsNullOrEmpty(Configuration.LoadWarning))
            {
                ConsoleHelper.Print($"Предупреждение: {Configuration.LoadWarning}");
            }

            var startupService = new StartupService(Configuration);
            var result = startupService.RunAsync(config).GetAwaiter().GetResult();

            var lines = new List<string>
            {
                $"Профиль: {Configuration.ActiveProfileName}",
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

    private static readonly string HelpText =
        "StartFlow — подготовка рабочего окружения при запуске." + Environment.NewLine +
        Environment.NewLine +
        "Использование:" + Environment.NewLine +
        "  StartFlow.exe             Открыть графический интерфейс." + Environment.NewLine +
        "  StartFlow.exe --silent    Выполнить активный профиль без окна и завершиться." + Environment.NewLine +
        "  StartFlow.exe --help      Показать эту справку." + Environment.NewLine +
        Environment.NewLine +
        "Пользовательские данные (профили, резервные копии, состояние) хранятся в:" + Environment.NewLine +
        "  %LOCALAPPDATA%\\StartFlow\\" + Environment.NewLine +
        Environment.NewLine +
        "Активный профиль можно переключить в настройках приложения.";
}