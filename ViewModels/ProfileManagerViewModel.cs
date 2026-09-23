using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StartFlow.Models;
using StartFlow.Services;

namespace StartFlow.ViewModels;

/// <summary>
/// Управление профилями в UI (страница Настройки):
/// выбор активного профиля, создание, копирование, переименование,
/// удаление, импорт и экспорт.
/// </summary>
public sealed class ProfileManagerViewModel : ObservableBase
{
    private readonly ProfileService _profiles;
    private readonly Action _activeProfileChanged;
    private ProfileInfo? _selectedProfile;

    private static XamlRoot? CurrentXamlRoot => App.MainWindowInstance?.Content?.XamlRoot;

    public ProfileManagerViewModel(ProfileService profiles, Action activeProfileChanged)
    {
        _profiles = profiles;
        _activeProfileChanged = activeProfileChanged;

        SelectProfileCommand = new RelayCommand(SelectProfile, CanSelectProfile);
        CreateProfileCommand = new AsyncRelayCommand(CreateProfileAsync);
        CopyProfileCommand = new AsyncRelayCommand(CopyProfileAsync, HasSelection);
        RenameProfileCommand = new AsyncRelayCommand(RenameProfileAsync, HasSelection);
        DeleteProfileCommand = new AsyncRelayCommand(DeleteProfileAsync, HasSelection);
        ExportProfileCommand = new AsyncRelayCommand(ExportProfileAsync, HasSelection);
        ImportProfileCommand = new AsyncRelayCommand(ImportProfileAsync);
    }

    public ObservableCollection<ProfileInfo> Profiles { get; } = new();

    public RelayCommand SelectProfileCommand { get; }

    public AsyncRelayCommand CreateProfileCommand { get; }

    public AsyncRelayCommand CopyProfileCommand { get; }

    public AsyncRelayCommand RenameProfileCommand { get; }

    public AsyncRelayCommand DeleteProfileCommand { get; }

    public AsyncRelayCommand ExportProfileCommand { get; }

    public AsyncRelayCommand ImportProfileCommand { get; }

    public ProfileInfo? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetField(ref _selectedProfile, value))
            {
                RaiseCanExecuteChanged();
            }
        }
    }

    public string ActiveProfileName => _profiles.GetActiveProfileName();

    public void Refresh()
    {
        var names = _profiles.ListProfileNames();
        var active = _profiles.GetActiveProfileName();
        var previousName = SelectedProfile?.Name;

        Profiles.Clear();
        foreach (var name in names)
        {
            Profiles.Add(new ProfileInfo(name, string.Equals(name, active, StringComparison.OrdinalIgnoreCase)));
        }

        SelectedProfile = Profiles.FirstOrDefault(p => string.Equals(p.Name, previousName, StringComparison.OrdinalIgnoreCase));
        OnPropertyChanged(nameof(ActiveProfileName));
        RaiseCanExecuteChanged();
    }

    private bool HasSelection() => SelectedProfile is not null;

    private bool CanSelectProfile() => SelectedProfile is not null
        && !string.Equals(SelectedProfile.Name, ActiveProfileName, StringComparison.OrdinalIgnoreCase);

    private void SelectProfile()
    {
        if (SelectedProfile is null || !CanSelectProfile())
        {
            return;
        }

        var result = _profiles.SetActiveProfile(SelectedProfile.Name);
        if (!result.Success)
        {
            ShowError(result.Error!);
            return;
        }

        Refresh();
        _activeProfileChanged();
    }

    private async Task CreateProfileAsync()
    {
        var name = await AskNameAsync("Новый профиль", "Имя нового профиля:", string.Empty);
        if (name is null)
        {
            return;
        }

        var result = _profiles.CreateProfile(name);
        if (!result.Success)
        {
            ShowError(result.Error!);
            return;
        }

        Refresh();
    }

    private async Task CopyProfileAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var suggested = SuggestAvailableName(SelectedProfile.Name + " (копия)");
        var name = await AskNameAsync("Копия профиля", $"Копия профиля «{SelectedProfile.Name}» будет создана под именем:", suggested);
        if (name is null)
        {
            return;
        }

        var result = _profiles.CopyProfile(SelectedProfile.Name, name);
        if (!result.Success)
        {
            ShowError(result.Error!);
            return;
        }

        Refresh();
    }

    private async Task RenameProfileAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var wasActive = SelectedProfile.IsActive;
        var name = await AskNameAsync("Переименовать профиль", "Новое имя профиля:", SelectedProfile.Name);
        if (name is null || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var result = _profiles.RenameProfile(SelectedProfile.Name, name);
        if (!result.Success)
        {
            ShowError(result.Error!);
            return;
        }

        Refresh();
        if (wasActive)
        {
            _activeProfileChanged();
        }
    }

    private async Task DeleteProfileAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var wasActive = SelectedProfile.IsActive;
        if (!await ConfirmAsync(
                "Удалить профиль",
                $"Удалить профиль «{SelectedProfile.Name}»? Его настройки будут потеряны."))
        {
            return;
        }

        var result = _profiles.DeleteProfile(SelectedProfile.Name);
        if (!result.Success)
        {
            ShowError(result.Error!);
            return;
        }

        Refresh();
        if (wasActive)
        {
            _activeProfileChanged();
        }
    }

    private async Task ExportProfileAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var target = await PickerService.PickSaveProfileAsync(SelectedProfile.Name + ".json");
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        var result = _profiles.ExportProfile(SelectedProfile.Name, target);
        if (!result.Success)
        {
            ShowError(result.Error!);
            return;
        }

        ShowInfo("Экспорт завершён", $"Профиль «{SelectedProfile.Name}» экспортирован в:\n{target}");
    }

    private async Task ImportProfileAsync()
    {
        var source = await PickerService.PickProfileFileAsync();
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        var result = _profiles.ImportProfile(source);

        while (!result.Success && result.NameConflict)
        {
            var resolution = await AskImportResolutionAsync(result.ImportedName ?? "Profile");
            if (resolution is null)
            {
                return;
            }

            var (name, replaceExisting) = resolution.Value;
            result = replaceExisting
                ? _profiles.ImportProfile(source, replaceExisting: true)
                : _profiles.ImportProfile(source, name);
        }

        if (!result.Success)
        {
            ShowError(result.Error!);
            return;
        }

        var importedBecameActive = result.ImportedName is not null
            && string.Equals(ActiveProfileName, result.ImportedName, StringComparison.OrdinalIgnoreCase);

        Refresh();
        if (importedBecameActive)
        {
            _activeProfileChanged();
        }
    }

    private string SuggestAvailableName(string name)
    {
        var candidate = name;
        var index = 2;
        while (_profiles.ListProfileNames().Any(n => string.Equals(n, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{name} ({index})";
            index++;
        }

        return candidate;
    }

    private void RaiseCanExecuteChanged()
    {
        SelectProfileCommand.RaiseCanExecuteChanged();
        CreateProfileCommand.RaiseCanExecuteChanged();
        CopyProfileCommand.RaiseCanExecuteChanged();
        RenameProfileCommand.RaiseCanExecuteChanged();
        DeleteProfileCommand.RaiseCanExecuteChanged();
        ExportProfileCommand.RaiseCanExecuteChanged();
        ImportProfileCommand.RaiseCanExecuteChanged();
    }

    private async Task<string?> AskNameAsync(string title, string header, string initialValue)
    {
        if (CurrentXamlRoot is null)
        {
            return null;
        }

        var box = new TextBox
        {
            Text = initialValue,
            PlaceholderText = "Имя профиля",
            SelectionStart = initialValue.Length,
            SelectionLength = 0
        };

        var dialog = new ContentDialog
        {
            Title = title,
            PrimaryButtonText = "ОК",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = CurrentXamlRoot,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = header, TextWrapping = TextWrapping.Wrap },
                    box
                }
            }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        return box.Text.Trim();
    }

    private async Task<(string? Name, bool ReplaceExisting)?> AskImportResolutionAsync(string conflictingName)
    {
        if (CurrentXamlRoot is null)
        {
            return null;
        }

        var box = new TextBox
        {
            Text = SuggestAvailableName(conflictingName + " (импорт)"),
            PlaceholderText = "Другое имя профиля"
        };

        var check = new CheckBox
        {
            Content = $"Заменить существующий профиль «{conflictingName}»",
            IsChecked = false
        };

        var dialog = new ContentDialog
        {
            Title = "Импорт профиля",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"Профиль с именем «{conflictingName}» уже существует. " +
                               "Укажите другое имя или замените существующий профиль.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    box,
                    check
                }
            },
            PrimaryButtonText = "Продолжить",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = CurrentXamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        if (check.IsChecked == true)
        {
            return (null, true);
        }

        var name = box.Text.Trim();
        return string.IsNullOrWhiteSpace(name) ? null : (name, false);
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        if (CurrentXamlRoot is null)
        {
            return false;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = "Удалить",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = CurrentXamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async void ShowError(string message)
    {
        if (CurrentXamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Ошибка",
            Content = message,
            CloseButtonText = "ОК",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = CurrentXamlRoot
        };

        try
        {
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private async void ShowInfo(string title, string message)
    {
        if (CurrentXamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "ОК",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = CurrentXamlRoot
        };

        try
        {
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
}
