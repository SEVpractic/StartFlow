using System.Text.Json;
using StartFlow.Models;

namespace StartFlow.Services;

/// <summary>
/// Фасад конфигурации приложения. Работает с активным профилем через ProfileService.
/// UI/ViewModels не знают ни путей, ни устройства хранения — они обращаются к этому сервису.
/// </summary>
public sealed class ConfigurationService
{
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly ProfileService _profiles;
    private ProfileModel? _activeProfile;

    public ProfileService Profiles { get; }

    public StartFlowConfig? CurrentConfig => _activeProfile?.Config;

    public string? LoadWarning { get; private set; }

    public string ActiveProfileName => _activeProfile?.Name ?? Profiles.GetActiveProfileName();

    public string DataDirectory => _profiles.RootPath;

    public string ProfilesDirectory => _profiles.ProfilesPath;

    public ConfigurationService()
    {
        var backup = new BackupService();
        _profiles = new ProfileService(backup, new MigrationService(backup));
        Profiles = _profiles;
    }

    /// <summary>Загружает настройки активного профиля (с миграцией схемы при необходимости).</summary>
    public StartFlowConfig Load()
    {
        if (_activeProfile is not null)
        {
            return _activeProfile.Config;
        }

        _activeProfile = _profiles.LoadActiveProfile(out var warning);
        LoadWarning = warning;
        return _activeProfile.Config;
    }

    public bool Save(StartFlowConfig config, out string? error)
    {
        if (_activeProfile is null)
        {
            Load();
        }

        if (!ReferenceEquals(_activeProfile!.Config, config))
        {
            _activeProfile.Config = config;
        }

        var result = _profiles.SaveActiveProfile(_activeProfile, out error);
        return result.Success;
    }

    public ProfileOperationResult SetActiveProfile(string name)
    {
        var result = _profiles.SetActiveProfile(name);
        if (result.Success)
        {
            InvalidateActiveProfile();
        }

        return result;
    }

    /// <summary>Сбрасывает кеш активного профиля; следующее обращение к Load() перечитает данные с диска.</summary>
    public void InvalidateActiveProfile()
    {
        _activeProfile = null;
        LoadWarning = null;
    }
}