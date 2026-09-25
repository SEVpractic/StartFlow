using System.Collections.ObjectModel;
using System.IO;
using Microsoft.UI.Xaml.Media;
using StartFlow.Models;
using StartFlow.Services;

namespace StartFlow.ViewModels;

public sealed class ProgramBadge
{
    public ProgramBadge(string glyph, string text)
    {
        Glyph = glyph;
        Text = text;
    }

    public string Glyph { get; }

    public string Text { get; }

    public bool HasGlyph => !string.IsNullOrEmpty(Glyph);
}

public sealed class ProgramCardItem : ObservableBase
{
    private ImageSource? _icon;

    public ProgramCardItem(string name, string path, IReadOnlyList<ProgramBadge> badges)
    {
        Name = name;
        Path = path;
        Badges = badges;
    }

    public string Name { get; }

    public string Path { get; }

    public IReadOnlyList<ProgramBadge> Badges { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? System.IO.Path.GetFileName(Path) : Name;

    public bool HasBadges => Badges.Count > 0;

    public ImageSource? Icon
    {
        get => _icon;
        set => SetField(ref _icon, value);
    }
}

public sealed class FolderGenerationCardItem
{
    public FolderGenerationCardItem(string label, string path)
    {
        Label = label;
        Path = path;
    }

    public string Label { get; }

    public string Path { get; }
}

public sealed class DashboardViewModel : ObservableBase
{
    private readonly ConfigurationService _configService;
    private readonly StartupService _startupService;

    private bool _isRunning;
    private bool _hasResult;
    private bool _hasErrors;
    private string? _resultTitle;
    private string? _resultSubtitle;
    private int _createdFolders;
    private int _openedFolders;
    private int _startedPrograms;
    private int _skippedPrograms;
    private int _errorCount;
    private bool _folderGenerationEnabled;
    private bool _hasOpenFolders;
    private bool _hasPrograms;
    private string? _loadWarning;
    private string? _activeProfileName;

    public DashboardViewModel(ConfigurationService configService, StartupService startupService)
    {
        _configService = configService;
        _startupService = startupService;
        RunCommand = new AsyncRelayCommand(ExecuteRunAsync);
    }

    public AsyncRelayCommand RunCommand { get; }

    public ObservableCollection<string> ErrorMessages { get; } = new();

    public ObservableCollection<string> OpenFolders { get; } = new();

    public ObservableCollection<ProgramCardItem> Programs { get; } = new();

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetField(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(CanRun));
            }
        }
    }

    public bool CanRun => !_isRunning;

    public bool HasResult { get => _hasResult; private set => SetField(ref _hasResult, value); }

    public bool HasErrors { get => _hasErrors; private set => SetField(ref _hasErrors, value); }

    public string? ResultTitle { get => _resultTitle; private set => SetField(ref _resultTitle, value); }

    public string? ResultSubtitle { get => _resultSubtitle; private set => SetField(ref _resultSubtitle, value); }

    public int CreatedFolders { get => _createdFolders; private set => SetField(ref _createdFolders, value); }

    public int OpenedFolders { get => _openedFolders; private set => SetField(ref _openedFolders, value); }

    public int StartedPrograms { get => _startedPrograms; private set => SetField(ref _startedPrograms, value); }

    public int SkippedPrograms { get => _skippedPrograms; private set => SetField(ref _skippedPrograms, value); }

    public int ErrorCount { get => _errorCount; private set => SetField(ref _errorCount, value); }

public bool FolderGenerationEnabled { get => _folderGenerationEnabled; private set => SetField(ref _folderGenerationEnabled, value); }

public ObservableCollection<FolderGenerationCardItem> FolderGenerations { get; } = new();

    public bool HasOpenFolders { get => _hasOpenFolders; private set => SetField(ref _hasOpenFolders, value); }

    public bool HasPrograms { get => _hasPrograms; private set => SetField(ref _hasPrograms, value); }

    public string? LoadWarning { get => _loadWarning; private set => SetField(ref _loadWarning, value); }

    public bool HasLoadWarning => !string.IsNullOrEmpty(LoadWarning);

    public string? ActiveProfileName { get => _activeProfileName; private set => SetField(ref _activeProfileName, value); }

    public void Refresh()
    {
        var config = _configService.Load() ?? StartFlowConfig.CreateDefault();

        LoadWarning = _configService.LoadWarning;
        OnPropertyChanged(nameof(HasLoadWarning));

        ActiveProfileName = _configService.ActiveProfileName;

        RefreshFolderGeneration(config);
        RefreshOpenFolders(config);
        RefreshPrograms(config);
    }

    private void RefreshFolderGeneration(StartFlowConfig config)
    {
        var items = new List<FolderGenerationCardItem>();

        foreach (var generation in config.FolderGeneration.Where(g => g is not null && g.Enabled))
        {
            if (string.IsNullOrWhiteSpace(generation.Path) || string.IsNullOrWhiteSpace(generation.Template))
            {
                continue;
            }

            var full = FolderService.ResolveTemplatePath(generation);
            items.Add(new FolderGenerationCardItem(
                Path.GetFileName(full),
                Path.GetDirectoryName(full) ?? generation.Path));
        }

        FolderGenerations.Clear();
        foreach (var item in items)
        {
            FolderGenerations.Add(item);
        }

        FolderGenerationEnabled = FolderGenerations.Count > 0;
    }

    private void RefreshOpenFolders(StartFlowConfig config)
    {
        OpenFolders.Clear();
        foreach (var folder in config.FoldersToOpen.Where(f => f.Enabled && !string.IsNullOrWhiteSpace(f.Path)))
        {
            OpenFolders.Add(folder.Path);
        }

        HasOpenFolders = OpenFolders.Count > 0;
    }

    private void RefreshPrograms(StartFlowConfig config)
    {
        Programs.Clear();

        const string shieldGlyph = "\uEA18";

        foreach (var program in config.Programs.Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Path)))
        {
            var badges = new List<ProgramBadge>();

            if (program.RunAsAdmin)
            {
                badges.Add(new ProgramBadge(shieldGlyph, "Запускать от имени администратора"));
            }

            if (program.SkipIfRunning && !string.IsNullOrWhiteSpace(program.ProcessName))
            {
                badges.Add(new ProgramBadge(string.Empty, "Не запускать, если уже работает"));
            }

            if (!program.SkipIfRunning)
            {
                badges.Add(new ProgramBadge(string.Empty, "Повторный запуск разрешён"));
            }

            var item = new ProgramCardItem(program.Name, program.Path, badges);
            Programs.Add(item);
            _ = LoadIconAsync(item);
        }

        HasPrograms = Programs.Count > 0;
    }

    private static async Task LoadIconAsync(ProgramCardItem item)
    {
        var icon = await IconImageSourceFactory.CreateFromPathAsync(item.Path);
        item.Icon = icon;
    }

    private async Task ExecuteRunAsync()
    {
        IsRunning = true;
        try
        {
            var config = _configService.Load() ?? StartFlowConfig.CreateDefault();
            var result = await _startupService.RunAsync(config);
            ApplyResult(result);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void ApplyResult(StartupResult result)
    {
        CreatedFolders = result.CreatedFolders;
        OpenedFolders = result.OpenedFolders;
        StartedPrograms = result.StartedPrograms;
        SkippedPrograms = result.SkippedPrograms;
        ErrorCount = result.Errors.Count;
        HasErrors = result.HasErrors;

        ResultTitle = result.HasErrors ? "Запуск завершён с ошибками" : "Запуск завершён";
        ResultSubtitle = result.HasErrors
            ? "Некоторые операции не удалось выполнить. См. список ниже."
            : "Рабочее окружение готово.";

        ErrorMessages.Clear();
        foreach (var error in result.Errors)
        {
            ErrorMessages.Add(error.ToString());
        }

        HasResult = true;
    }
}