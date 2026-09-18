using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StartFlow.Models;
using StartFlow.Services;

namespace StartFlow.ViewModels;

public sealed class FolderGenerationEditor : ObservableBase
{
    private readonly FolderGenerationConfig _model;
    private readonly Action<FolderGenerationEditor> _requestRemove;

    public FolderGenerationEditor(FolderGenerationConfig model, Action<FolderGenerationEditor> requestRemove)
    {
        _model = model;
        _requestRemove = requestRemove;
        ChoosePathCommand = new AsyncRelayCommand(ChoosePathAsync);
        DeleteCommand = new RelayCommand(() => _requestRemove(this));
    }

    public AsyncRelayCommand ChoosePathCommand { get; }

    public RelayCommand DeleteCommand { get; }

    public FolderGenerationConfig Model => _model;

    public bool Enabled
    {
        get => _model.Enabled;
        set
        {
            _model.Enabled = value;
            OnPropertyChanged();
        }
    }

    public bool OpenAfterCreate
    {
        get => _model.OpenAfterCreate;
        set
        {
            _model.OpenAfterCreate = value;
            OnPropertyChanged();
        }
    }

    public string Path
    {
        get => _model.Path;
        set
        {
            _model.Path = value?.Trim() ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public string Template
    {
        get => _model.Template;
        set
        {
            _model.Template = value?.Trim() ?? string.Empty;
            OnPropertyChanged();
        }
    }

    private async Task ChoosePathAsync()
    {
        var path = await PickerService.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            Path = path;
        }
    }
}

public sealed class FolderToOpenEditor : ObservableBase
{
    private readonly FolderToOpen _model;
    private readonly Action<FolderToOpenEditor> _requestRemove;

    public FolderToOpenEditor(FolderToOpen model, Action<FolderToOpenEditor> requestRemove)
    {
        _model = model;
        _requestRemove = requestRemove;
        ChoosePathCommand = new AsyncRelayCommand(ChoosePathAsync);
        DeleteCommand = new RelayCommand(() => _requestRemove(this));
    }

    public AsyncRelayCommand ChoosePathCommand { get; }

    public RelayCommand DeleteCommand { get; }

    public FolderToOpen Model => _model;

    public string Path
    {
        get => _model.Path;
        set
        {
            _model.Path = value?.Trim() ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public bool Enabled
    {
        get => _model.Enabled;
        set
        {
            _model.Enabled = value;
            OnPropertyChanged();
        }
    }

    private async Task ChoosePathAsync()
    {
        var path = await PickerService.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            Path = path;
        }
    }
}

public sealed class ProgramEditor : ObservableBase
{
    private readonly ProgramConfig _model;
    private readonly Action<ProgramEditor> _requestRemove;
    private string _nameWasAuto;

    public ProgramEditor(ProgramConfig model, Action<ProgramEditor> requestRemove)
    {
        _model = model;
        _requestRemove = requestRemove;
        _nameWasAuto = model.Name;
        ChooseExeCommand = new AsyncRelayCommand(ChooseExeAsync);
        DeleteCommand = new RelayCommand(() => _requestRemove(this));
    }

    public AsyncRelayCommand ChooseExeCommand { get; }

    public RelayCommand DeleteCommand { get; }

    public ProgramConfig Model => _model;

    public string Name
    {
        get => _model.Name;
        set
        {
            _model.Name = value?.Trim() ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public string Path
    {
        get => _model.Path;
        set
        {
            _model.Path = value?.Trim() ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public string Args
    {
        get => _model.Args;
        set
        {
            _model.Args = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public bool RunAsAdmin
    {
        get => _model.RunAsAdmin;
        set
        {
            _model.RunAsAdmin = value;
            OnPropertyChanged();
        }
    }

    public bool SkipIfRunning
    {
        get => _model.SkipIfRunning;
        set
        {
            _model.SkipIfRunning = value;
            OnPropertyChanged();
        }
    }

    public string ProcessName
    {
        get => _model.ProcessName;
        set
        {
            _model.ProcessName = value?.Trim() ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public bool Enabled
    {
        get => _model.Enabled;
        set
        {
            _model.Enabled = value;
            OnPropertyChanged();
        }
    }

    private async Task ChooseExeAsync()
    {
        var chosen = await PickerService.PickExecutableAsync();
        if (string.IsNullOrWhiteSpace(chosen))
        {
            return;
        }

        var baseName = System.IO.Path.GetFileNameWithoutExtension(chosen);
        var shouldUpdateName = string.IsNullOrWhiteSpace(Name)
            || string.Equals(Name, _nameWasAuto, StringComparison.OrdinalIgnoreCase);

        var currentProcessName = System.IO.Path.GetFileNameWithoutExtension(Path);
        var shouldUpdateProcessName = string.IsNullOrWhiteSpace(ProcessName)
            || string.Equals(ProcessName, currentProcessName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ProcessName, _nameWasAuto, StringComparison.OrdinalIgnoreCase);

        Path = chosen;

        if (shouldUpdateName)
        {
            Name = baseName;
        }

        if (shouldUpdateProcessName)
        {
            ProcessName = baseName;
        }

        _nameWasAuto = baseName;
    }
}

public sealed class SettingsViewModel : ObservableBase
{
    private const string ThemeSystem = "default";
    private const string ThemeLight = "light";
    private const string ThemeDark = "dark";

    private readonly ConfigurationService _configService;
    private StartFlowConfig _config = StartFlowConfig.CreateDefault();

    private int _selectedThemeIndex;
    private bool _isSaveInfoVisible;
    private string? _saveInfoText;
    private string? _loadWarning;

    public SettingsViewModel(ConfigurationService configService)
    {
        _configService = configService;
        AddFolderCommand = new AsyncRelayCommand(AddFolderAsync);
        AddFolderGenerationCommand = new AsyncRelayCommand(AddFolderGenerationAsync);
        AddProgramCommand = new AsyncRelayCommand(AddProgramAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public AsyncRelayCommand AddFolderCommand { get; }

    public AsyncRelayCommand AddFolderGenerationCommand { get; }

    public AsyncRelayCommand AddProgramCommand { get; }

    public AsyncRelayCommand SaveCommand { get; }

    public ObservableCollection<FolderToOpenEditor> Folders { get; } = new();

    public ObservableCollection<ProgramEditor> Programs { get; } = new();

    public ObservableCollection<FolderGenerationEditor> FolderGeneration { get; } = new();

    public XamlRoot? XamlRoot { get; set; }

    public int SelectedThemeIndex
    {
        get => _selectedThemeIndex;
        set
        {
            if (SetField(ref _selectedThemeIndex, value))
            {
                _config.Theme = value switch
                {
                    1 => ThemeLight,
                    2 => ThemeDark,
                    _ => ThemeSystem
                };
                App.MainWindowInstance?.ApplyTheme(_config.Theme);
            }
        }
    }

    public bool IsSaveInfoVisible
    {
        get => _isSaveInfoVisible;
        private set => SetField(ref _isSaveInfoVisible, value);
    }

    public string? SaveInfoText
    {
        get => _saveInfoText;
        private set => SetField(ref _saveInfoText, value);
    }

    public string? LoadWarning
    {
        get => _loadWarning;
        private set => SetField(ref _loadWarning, value);
    }

    public bool HasLoadWarning => !string.IsNullOrEmpty(LoadWarning);

    public void Load()
    {
        _config = _configService.Load() ?? StartFlowConfig.CreateDefault();

        LoadWarning = _configService.LoadWarning;
        OnPropertyChanged(nameof(HasLoadWarning));

        FolderGeneration.Clear();
        foreach (var generation in _config.FolderGeneration)
        {
            FolderGeneration.Add(new FolderGenerationEditor(generation, RequestRemoveFolderGeneration));
        }
        OnPropertyChanged(nameof(FolderGeneration));

        Folders.Clear();
        foreach (var folder in _config.FoldersToOpen)
        {
            Folders.Add(new FolderToOpenEditor(folder, RequestRemoveFolder));
        }

        Programs.Clear();
        foreach (var program in _config.Programs)
        {
            Programs.Add(new ProgramEditor(program, RequestRemoveProgram));
        }

        SelectedThemeIndex = _config.Theme switch
        {
            ThemeLight => 1,
            ThemeDark => 2,
            _ => 0
        };
    }

    private async Task AddFolderAsync()
    {
        var path = await PickerService.PickFolderAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var model = new FolderToOpen { Path = path, Enabled = true };
        _config.FoldersToOpen.Add(model);
        Folders.Add(new FolderToOpenEditor(model, RequestRemoveFolder));
    }

    private async Task AddFolderGenerationAsync()
    {
        var path = await PickerService.PickFolderAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var model = new FolderGenerationConfig
        {
            Enabled = true,
            OpenAfterCreate = true,
            Path = path,
            Template = "Homework {date}"
        };
        _config.FolderGeneration.Add(model);
        FolderGeneration.Add(new FolderGenerationEditor(model, RequestRemoveFolderGeneration));
    }

    private async Task AddProgramAsync()
    {
        var path = await PickerService.PickExecutableAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        var model = new ProgramConfig
        {
            Name = name,
            Path = path,
            ProcessName = name,
            Enabled = true
        };
        _config.Programs.Add(model);
        Programs.Add(new ProgramEditor(model, RequestRemoveProgram));
    }

    private async void RequestRemoveFolder(FolderToOpenEditor editor)
    {
        if (XamlRoot is not null)
        {
            var dialog = new ContentDialog
            {
                Title = "Удалить папку",
                Content = $"Удалить «{editor.Path}» из списка?",
                PrimaryButtonText = "Удалить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        _config.FoldersToOpen.Remove(editor.Model);
        Folders.Remove(editor);
    }

    private async void RequestRemoveFolderGeneration(FolderGenerationEditor editor)
    {
        if (XamlRoot is not null)
        {
            var dialog = new ContentDialog
            {
                Title = "Удалить генерацию",
                Content = $"Удалить «{editor.Path}» из списка?",
                PrimaryButtonText = "Удалить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        _config.FolderGeneration.Remove(editor.Model);
        FolderGeneration.Remove(editor);
    }

    private async void RequestRemoveProgram(ProgramEditor editor)
    {
        if (XamlRoot is not null)
        {
            var dialog = new ContentDialog
            {
                Title = "Удалить приложение",
                Content = $"Удалить «{editor.Name}» из списка?",
                PrimaryButtonText = "Удалить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        _config.Programs.Remove(editor.Model);
        Programs.Remove(editor);
    }

    private async Task SaveAsync()
    {
        if (_configService.Save(_config, out var error))
        {
            SaveInfoText = "Настройки сохранены.";
        }
        else
        {
            SaveInfoText = $"Не удалось сохранить настройки: {error}";
        }

        IsSaveInfoVisible = true;
        await Task.Delay(3000);
        IsSaveInfoVisible = false;
    }
}