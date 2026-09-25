using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using StartFlow.Models;
using StartFlow.Services;
using StartFlow.Views;

namespace StartFlow.ViewModels;

internal enum AddProgramMode
{
    None,
    Installed,
    Manual
}

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
    private ImageSource? _icon;
    private CancellationTokenSource? _iconLoadCts;

    public ProgramEditor(ProgramConfig model, Action<ProgramEditor> requestRemove)
    {
        _model = model;
        _requestRemove = requestRemove;
        _nameWasAuto = model.Name;
        ChooseExeCommand = new AsyncRelayCommand(ChooseExeAsync);
        ChooseWorkingDirectoryCommand = new AsyncRelayCommand(ChooseWorkingDirectoryAsync);
        DeleteCommand = new RelayCommand(() => _requestRemove(this));
        ScheduleIconLoad();
    }

    public ImageSource? Icon
    {
        get => _icon;
        private set => SetField(ref _icon, value);
    }

    public AsyncRelayCommand ChooseExeCommand { get; }

    public AsyncRelayCommand ChooseWorkingDirectoryCommand { get; }

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
            ScheduleIconLoad();
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

    public string WorkingDirectory
    {
        get => _model.WorkingDirectory;
        set
        {
            _model.WorkingDirectory = value?.Trim() ?? string.Empty;
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

    private void ScheduleIconLoad()
    {
        _iconLoadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _iconLoadCts = cts;
        _ = LoadIconAsync(cts.Token);
    }

    private async Task LoadIconAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(200, token);
            var bytes = await Task.Run(() => InstalledApplicationsService.ExtractIconBytes(Path));
            token.ThrowIfCancellationRequested();
            Icon = await IconImageSourceFactory.CreateFromPixelDataAsync(bytes);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (!token.IsCancellationRequested)
            {
                Icon = null;
            }
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

    private async Task ChooseWorkingDirectoryAsync()
    {
        var path = await PickerService.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            WorkingDirectory = path;
        }
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
    private bool _isLoading;
    private string? _loadWarning;

    public SettingsViewModel(ConfigurationService configService)
    {
        _configService = configService;
        Profiles = new ProfileManagerViewModel(configService.Profiles, ReloadAfterProfileChange);
        AddFolderCommand = new AsyncRelayCommand(AddFolderAsync);
        AddFolderGenerationCommand = new AsyncRelayCommand(AddFolderGenerationAsync);
        AddProgramCommand = new AsyncRelayCommand(AddProgramAsync);
    }

    public AsyncRelayCommand AddFolderCommand { get; }

    public AsyncRelayCommand AddFolderGenerationCommand { get; }

    public AsyncRelayCommand AddProgramCommand { get; }

    public ProfileManagerViewModel Profiles { get; }

    private void ReloadAfterProfileChange()
    {
        _configService.InvalidateActiveProfile();
        Load();
    }

    public ObservableCollection<FolderToOpenEditor> Folders { get; } = new();

    public ObservableCollection<ProgramEditor> Programs { get; } = new();

    public ObservableCollection<FolderGenerationEditor> FolderGeneration { get; } = new();

    private static XamlRoot? CurrentXamlRoot => App.MainWindowInstance?.Content?.XamlRoot;

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
                if (!_isLoading)
                {
                    AutoSave();
                }
            }
        }
    }

    public string? LoadWarning
    {
        get => _loadWarning;
        private set => SetField(ref _loadWarning, value);
    }

    public bool HasLoadWarning => !string.IsNullOrEmpty(LoadWarning);

    public void Load()
    {
        _isLoading = true;
        try
        {
            _config = _configService.Load() ?? StartFlowConfig.CreateDefault();

            LoadWarning = _configService.LoadWarning;
            OnPropertyChanged(nameof(HasLoadWarning));

            FolderGeneration.Clear();
            foreach (var generation in _config.FolderGeneration)
            {
                FolderGeneration.Add(AttachAutoSave(new FolderGenerationEditor(generation, RequestRemoveFolderGeneration)));
            }
            OnPropertyChanged(nameof(FolderGeneration));

            Folders.Clear();
            foreach (var folder in _config.FoldersToOpen)
            {
                Folders.Add(AttachAutoSave(new FolderToOpenEditor(folder, RequestRemoveFolder)));
            }

            Programs.Clear();
            foreach (var program in _config.Programs)
            {
                Programs.Add(AttachAutoSave(new ProgramEditor(program, RequestRemoveProgram)));
            }

            SelectedThemeIndex = _config.Theme switch
            {
                ThemeLight => 1,
                ThemeDark => 2,
                _ => 0
            };

            Profiles.Refresh();
        }
        finally
        {
            _isLoading = false;
        }
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
        Folders.Add(AttachAutoSave(new FolderToOpenEditor(model, RequestRemoveFolder)));
        AutoSave();
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
            Template = "{date}"
        };
        _config.FolderGeneration.Add(model);
        FolderGeneration.Add(AttachAutoSave(new FolderGenerationEditor(model, RequestRemoveFolderGeneration)));
        AutoSave();
    }

    private async Task AddProgramAsync()
    {
        var mode = await AskAddProgramModeAsync();
        if (mode == AddProgramMode.None)
        {
            return;
        }

        if (mode == AddProgramMode.Manual)
        {
            await AddManualProgramAsync();
            return;
        }

        await AddInstalledProgramAsync();
    }

    private async Task<AddProgramMode> AskAddProgramModeAsync()
    {
        var installedButton = new Button
        {
            Content = "Выбрать из установленных приложений",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var manualButton = new Button
        {
            Content = "Найти exe вручную",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var content = new StackPanel { Spacing = 8, MinWidth = 380 };
        content.Children.Add(installedButton);
        content.Children.Add(manualButton);

        var mode = AddProgramMode.None;
        var dialog = new ContentDialog
        {
            Title = "Добавить приложение",
            Content = content,
            CloseButtonText = "Отмена",
            XamlRoot = CurrentXamlRoot
        };

        installedButton.Click += (_, _) =>
        {
            mode = AddProgramMode.Installed;
            dialog.Hide();
        };
        manualButton.Click += (_, _) =>
        {
            mode = AddProgramMode.Manual;
            dialog.Hide();
        };

        await dialog.ShowAsync();
        return mode;
    }

    private async Task AddInstalledProgramAsync()
    {
        var pickerViewModel = new InstalledAppsPickerViewModel();
        var picker = new InstalledAppsPickerView { DataContext = pickerViewModel };

        var dialog = new ContentDialog
        {
            Title = "Выбор установленного приложения",
            Content = picker,
            PrimaryButtonText = "Выбрать",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
            XamlRoot = CurrentXamlRoot
        };

        pickerViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(InstalledAppsPickerViewModel.SelectedItem))
            {
                dialog.IsPrimaryButtonEnabled = pickerViewModel.SelectedItem is { CanAddAsProgram: true };
            }
        };

        var showDialog = dialog.ShowAsync();
        await pickerViewModel.LoadAsync();
        var result = await showDialog;

        if (result == ContentDialogResult.Primary
            && pickerViewModel.SelectedItem is { CanAddAsProgram: true })
        {
            AddProgramFromInstalled(pickerViewModel.SelectedItem.Source);
        }
    }

    private void AddProgramFromInstalled(InstalledApplication application)
    {
        if (!application.CanAddAsProgram || string.IsNullOrWhiteSpace(application.ExecutablePath))
        {
            return;
        }

        var exe = application.ExecutablePath;
        var isExe = string.Equals(Path.GetExtension(exe), ".exe", StringComparison.OrdinalIgnoreCase);
        var name = string.IsNullOrWhiteSpace(application.Name)
            ? Path.GetFileNameWithoutExtension(exe)
            : application.Name.Trim();

        var model = new ProgramConfig
        {
            Name = name,
            Path = exe,
            Args = application.LaunchArguments ?? string.Empty,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? string.Empty,
            ProcessName = isExe ? Path.GetFileNameWithoutExtension(exe) : string.Empty,
            Enabled = true,
            RunAsAdmin = false,
            SkipIfRunning = false
        };

        _config.Programs.Add(model);
        Programs.Add(AttachAutoSave(new ProgramEditor(model, RequestRemoveProgram)));
        AutoSave();
    }

    private async Task AddManualProgramAsync()
    {
        var path = await PickerService.PickExecutableAsync();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var name = Path.GetFileNameWithoutExtension(path);
        var model = new ProgramConfig
        {
            Name = name,
            Path = path,
            WorkingDirectory = Path.GetDirectoryName(path) ?? string.Empty,
            ProcessName = name,
            Enabled = true
        };
        _config.Programs.Add(model);
        Programs.Add(AttachAutoSave(new ProgramEditor(model, RequestRemoveProgram)));
        AutoSave();
    }

    private async void RequestRemoveFolder(FolderToOpenEditor editor)
    {
        if (CurrentXamlRoot is not null)
        {
            var dialog = new ContentDialog
            {
                Title = "Удалить папку",
                Content = $"Удалить «{editor.Path}» из списка?",
                PrimaryButtonText = "Удалить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = CurrentXamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        _config.FoldersToOpen.Remove(editor.Model);
        Folders.Remove(editor);
        AutoSave();
    }

    private async void RequestRemoveFolderGeneration(FolderGenerationEditor editor)
    {
        if (CurrentXamlRoot is not null)
        {
            var dialog = new ContentDialog
            {
                Title = "Удалить генерацию",
                Content = $"Удалить «{editor.Path}» из списка?",
                PrimaryButtonText = "Удалить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = CurrentXamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        _config.FolderGeneration.Remove(editor.Model);
        FolderGeneration.Remove(editor);
        AutoSave();
    }

    private async void RequestRemoveProgram(ProgramEditor editor)
    {
        if (CurrentXamlRoot is not null)
        {
            var dialog = new ContentDialog
            {
                Title = "Удалить приложение",
                Content = $"Удалить «{editor.Name}» из списка?",
                PrimaryButtonText = "Удалить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = CurrentXamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        _config.Programs.Remove(editor.Model);
        Programs.Remove(editor);
        AutoSave();
    }

    private T AttachAutoSave<T>(T editor) where T : ObservableBase
    {
        editor.PropertyChanged += OnEditorPropertyChanged;
        return editor;
    }

    private void AutoSave()
    {
        _configService.Save(_config, out _);
    }

    private void OnEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!_isLoading)
        {
            AutoSave();
        }
    }
}
