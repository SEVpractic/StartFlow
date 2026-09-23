using System.Text.Json;
using StartFlow.Models;

namespace StartFlow.Services;

/// <summary>
/// Управление профилями: каталог profiles, активный профиль (state.json),
/// создание, копирование, переименование, удаление, импорт/экспорт и миграции.
///
/// Совокупность физических путей сосредоточена здесь; UI/ViewModel не знают
/// конкретных путей к файлам.
/// </summary>
public sealed class ProfileService
{
    private readonly BackupService _backup;
    private readonly MigrationService _migration;

    public ProfileService(BackupService backup, MigrationService migration)
    {
        _backup = backup;
        _migration = migration;
    }

    public string RootPath => AppDataPaths.Root;

    public string ProfilesPath => AppDataPaths.ProfilesDirectory;

    /// <summary>
    /// Создаёт структуру каталогов и начальные данные при первом запуске:
    /// Default-профиль и state.json с активным профилем. Существующие
    /// профили никогда не перезаписываются. Идемпотентно.
    /// </summary>
    public void EnsureInitialized()
    {
        Directory.CreateDirectory(AppDataPaths.Root);
        Directory.CreateDirectory(AppDataPaths.ProfilesDirectory);
        Directory.CreateDirectory(AppDataPaths.BackupsDirectory);
        Directory.CreateDirectory(AppDataPaths.LogsDirectory);

        var names = ReadProfileNames();
        if (names.Count == 0)
        {
            SaveProfile(ProfileModel.CreateNew("Default"));
            names = new List<string> { "Default" };
        }

        var state = LoadState();
        if (state is null || string.IsNullOrWhiteSpace(state.ActiveProfile) || !ContainsName(names, state.ActiveProfile))
        {
            var active = names.FirstOrDefault(n => string.Equals(n, "Default", StringComparison.OrdinalIgnoreCase))
                         ?? names[0];
            SaveState(new AppState { ActiveProfile = active });
        }
    }

    public IReadOnlyList<string> ListProfileNames()
    {
        EnsureInitialized();
        return ReadProfileNames();
    }

    public string GetActiveProfileName()
    {
        EnsureInitialized();
        return LoadState()?.ActiveProfile ?? ListProfileNames().FirstOrDefault() ?? "Default";
    }

    public ProfileModel LoadActiveProfile(out string? warning)
    {
        EnsureInitialized();
        warning = null;

        var names = ReadProfileNames();
        if (names.Count == 0)
        {
            var fresh = ProfileModel.CreateNew("Default");
            SaveProfile(fresh);
            SaveState(new AppState { ActiveProfile = fresh.Name });
            return fresh;
        }

        var state = LoadState() ?? new AppState();
        var active = string.IsNullOrWhiteSpace(state.ActiveProfile) ? "Default" : state.ActiveProfile;

        var path = GetProfilePath(active);
        if (!File.Exists(path))
        {
            var fallback = names.FirstOrDefault(n => string.Equals(n, "Default", StringComparison.OrdinalIgnoreCase))
                           ?? names.First();
            if (!string.Equals(active, fallback, StringComparison.OrdinalIgnoreCase))
            {
                warning = $"Профиль «{active}» не найден. Активным назначен профиль «{fallback}».";
            }

            state.ActiveProfile = fallback;
            SaveState(state);
            active = fallback;
            path = GetProfilePath(active);
        }

        var profile = ReadAndMigrate(path, out var readWarning);
        if (warning is null)
        {
            warning = readWarning;
        }

        return profile;
    }

    /// <summary>Загружает и, при необходимости, мигрирует профиль (для копирования/экспорта/переименования).</summary>
    public ProfileModel LoadProfileForRead(string name, out string? warning)
    {
        EnsureInitialized();
        var path = GetProfilePath(name);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Профиль «{name}» не найден.");
        }

        return ReadAndMigrate(path, out warning);
    }

    public ProfileOperationResult SaveActiveProfile(ProfileModel profile, out string? error)
    {
        var result = SaveProfile(profile);
        error = result.Error;
        return result;
    }

    public ProfileOperationResult CreateProfile(string name)
    {
        EnsureInitialized();
        if (!ProfileNameValidator.TryValidate(name, out var error))
        {
            return ProfileOperationResult.Fail(error!);
        }

        if (File.Exists(GetProfilePath(name)))
        {
            return ProfileOperationResult.Fail($"Профиль с именем «{name}» уже существует.");
        }

        return SaveProfile(ProfileModel.CreateNew(name));
    }

    public ProfileOperationResult CopyProfile(string sourceName, string newName)
    {
        EnsureInitialized();
        if (!ProfileNameValidator.TryValidate(newName, out var error))
        {
            return ProfileOperationResult.Fail(error!);
        }

        if (File.Exists(GetProfilePath(newName)))
        {
            return ProfileOperationResult.Fail($"Профиль с именем «{newName}» уже существует.");
        }

        var sourcePath = GetProfilePath(sourceName);
        if (!File.Exists(sourcePath))
        {
            return ProfileOperationResult.Fail($"Профиль «{sourceName}» не найден.");
        }

        var source = ReadAndMigrate(sourcePath, out _);
        var copy = new ProfileModel
        {
            SchemaVersion = SchemaRegistry.CurrentSchemaVersion,
            Name = newName,
            Config = DeepCopy(source.Config)
        };

        return SaveProfile(copy);
    }

    public ProfileOperationResult RenameProfile(string oldName, string newName)
    {
        EnsureInitialized();
        if (!ProfileNameValidator.TryValidate(newName, out var error))
        {
            return ProfileOperationResult.Fail(error!);
        }

        if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
        {
            return ProfileOperationResult.SuccessResult;
        }

        var oldPath = GetProfilePath(oldName);
        if (!File.Exists(oldPath))
        {
            return ProfileOperationResult.Fail($"Профиль «{oldName}» не найден.");
        }

        var newPath = GetProfilePath(newName);
        if (File.Exists(newPath))
        {
            return ProfileOperationResult.Fail($"Имя «{newName}» уже занято другим профилем.");
        }

        var profile = ReadAndMigrate(oldPath, out _);
        profile.Name = newName;

        var result = SaveProfile(profile, newPath);
        if (!result.Success)
        {
            return result;
        }

        try
        {
            File.Delete(oldPath);
        }
        catch (Exception ex)
        {
            return ProfileOperationResult.Fail("Профиль сохранён под новым именем, но не удалось удалить старый файл: " + ex.Message);
        }

        var state = LoadState();
        if (state is not null && string.Equals(state.ActiveProfile, oldName, StringComparison.OrdinalIgnoreCase))
        {
            state.ActiveProfile = newName;
            SaveState(state);
        }

        return ProfileOperationResult.SuccessResult;
    }

    public ProfileOperationResult DeleteProfile(string name)
    {
        EnsureInitialized();
        var names = ReadProfileNames();
        if (names.Count <= 1)
        {
            return ProfileOperationResult.Fail("Нельзя удалить последний профиль StartFlow.");
        }

        var path = GetProfilePath(name);
        if (!File.Exists(path))
        {
            return ProfileOperationResult.Fail($"Профиль «{name}» не найден.");
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            return ProfileOperationResult.Fail("Не удалось удалить профиль: " + ex.Message);
        }

        var state = LoadState();
        if (state is not null && string.Equals(state.ActiveProfile, name, StringComparison.OrdinalIgnoreCase))
        {
            var remaining = ReadProfileNames();
            state.ActiveProfile = remaining.FirstOrDefault() ?? "Default";
            SaveState(state);
        }

        return ProfileOperationResult.SuccessResult;
    }

    public ProfileOperationResult SetActiveProfile(string name)
    {
        EnsureInitialized();
        if (!ContainsName(ReadProfileNames(), name))
        {
            return ProfileOperationResult.Fail($"Профиль «{name}» не найден.");
        }

        var state = LoadState() ?? new AppState();
        if (string.Equals(state.ActiveProfile, name, StringComparison.OrdinalIgnoreCase))
        {
            return ProfileOperationResult.SuccessResult;
        }

        state.ActiveProfile = name;
        return SaveState(state);
    }

    public ProfileOperationResult ExportProfile(string name, string targetPath)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return ProfileOperationResult.Fail("Не указан путь для экспорта профиля.");
        }

        var path = GetProfilePath(name);
        if (!File.Exists(path))
        {
            return ProfileOperationResult.Fail($"Профиль «{name}» не найден.");
        }

        var profile = ReadAndMigrate(path, out _);
        try
        {
            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(targetPath, JsonSerializer.Serialize(profile, ConfigurationService.JsonOptions));
            return ProfileOperationResult.SuccessResult;
        }
        catch (Exception ex)
        {
            return ProfileOperationResult.Fail("Не удалось экспортировать профиль: " + ex.Message);
        }
    }

    public ImportProfileResult ImportProfile(string sourcePath, string? overrideName = null, bool replaceExisting = false)
    {
        EnsureInitialized();
        try
        {
            var json = File.ReadAllText(sourcePath);
            ValidateEnvelope(json);

            var imported = JsonSerializer.Deserialize<ProfileModel>(json, ConfigurationService.JsonOptions)
                           ?? throw new JsonException("Файл не содержит профиль.");
            imported.Config ??= StartFlowConfig.CreateDefault();

            var profileName = string.IsNullOrWhiteSpace(overrideName) ? imported.Name : overrideName!.Trim();
            if (!ProfileNameValidator.TryValidate(profileName, out var nameError))
            {
                return ImportProfileResult.Fail("Некорректное имя профиля: " + nameError);
            }

            if (imported.SchemaVersion != SchemaRegistry.CurrentSchemaVersion)
            {
                try
                {
                    imported = _migration.Migrate(imported, sourcePath, json);
                }
                catch (Exception ex)
                {
                    return ImportProfileResult.Fail(
                        "Не удалось выполнить миграцию импортируемого профиля: " + ex.Message +
                        ". Исходный файл не изменён.");
                }
            }

            imported.Name = profileName;

            var target = GetProfilePath(profileName);
            if (File.Exists(target) && !replaceExisting)
            {
                return new ImportProfileResult
                {
                    Success = false,
                    NameConflict = true,
                    ImportedName = profileName
                };
            }

            var result = SaveProfile(imported, target);
            return new ImportProfileResult
            {
                Success = result.Success,
                Error = result.Error,
                ImportedName = result.Success ? profileName : null
            };
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return ImportProfileResult.Fail("Не удалось прочитать импортируемый файл: " + ex.Message);
        }
    }

    private ProfileOperationResult SaveProfile(ProfileModel profile, string? path = null)
    {
        var target = path ?? GetProfilePath(profile.Name);
        try
        {
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(target, JsonSerializer.Serialize(profile, ConfigurationService.JsonOptions));
            return ProfileOperationResult.SuccessResult;
        }
        catch (Exception ex)
        {
            return ProfileOperationResult.Fail("Не удалось сохранить профиль: " + ex.Message);
        }
    }

    /// <summary>Читает файл, при повреждении — карантинирует и восстанавливает настройки по умолчанию; при необходимости мигрирует.</summary>
    private ProfileModel ReadAndMigrate(string path, out string? warning)
    {
        warning = null;
        try
        {
            var profile = ReadProfile(path);
            if (profile.SchemaVersion == SchemaRegistry.CurrentSchemaVersion)
            {
                return profile;
            }

            if (profile.SchemaVersion > SchemaRegistry.CurrentSchemaVersion)
            {
                warning = $"Профиль использует более новую версию схемы ({profile.SchemaVersion}), чем поддерживает " +
                          $"эта версия StartFlow ({SchemaRegistry.CurrentSchemaVersion}). Профиль будет открыт без изменений.";
                return profile;
            }

            var raw = File.ReadAllText(path);
            try
            {
                var migrated = _migration.Migrate(profile, path, raw);
                SaveProfile(migrated, path);
                return migrated;
            }
            catch (Exception ex)
            {
                Quarantine(path);
                var fresh = ProfileModel.CreateNew(Path.GetFileNameWithoutExtension(path));
                SaveProfile(fresh, path);
                warning = $"Не удалось обновить схему профиля: {ex.Message} Исходный профиль сохранён в резервной копии. " +
                          "На его месте создан профиль с настройками по умолчанию.";
                return fresh;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Quarantine(path);
            var fresh = ProfileModel.CreateNew(Path.GetFileNameWithoutExtension(path));
            SaveProfile(fresh, path);
            warning = "Профиль повреждён и не может быть прочитан. Профиль восстановлен с настройками по умолчанию. " +
                      "Испорченный файл сохранён в резервной копии.";
            return fresh;
        }
    }

    private static ProfileModel ReadProfile(string path)
    {
        var json = File.ReadAllText(path);
        var profile = JsonSerializer.Deserialize<ProfileModel>(json, ConfigurationService.JsonOptions)
                      ?? throw new JsonException("Профиль пуст.");

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            profile.Name = Path.GetFileNameWithoutExtension(path);
        }

        profile.Config ??= StartFlowConfig.CreateDefault();
        return profile;
    }

    private static void ValidateEnvelope(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Файл не является профилем StartFlow.");
        }

        if (!root.TryGetProperty("schemaVersion", out var version) || version.ValueKind != JsonValueKind.Number)
        {
            throw new JsonException("Файл не содержит корректное поле schemaVersion — это не профиль StartFlow.");
        }

        if (!root.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("Файл не содержит имя профиля.");
        }

        if (!root.TryGetProperty("config", out var config) || config.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Файл не содержит блок настроек config.");
        }
    }

    private static StartFlowConfig DeepCopy(StartFlowConfig config)
    {
        var json = JsonSerializer.Serialize(config, ConfigurationService.JsonOptions);
        return JsonSerializer.Deserialize<StartFlowConfig>(json, ConfigurationService.JsonOptions)
               ?? StartFlowConfig.CreateDefault();
    }

    private void Quarantine(string path)
    {
        try
        {
            _backup.CopyFileToBackups(path, "bad");
        }
        catch
        {
            // Карантин — best effort.
        }
    }

    private static List<string> ReadProfileNames()
        => Directory.Exists(AppDataPaths.ProfilesDirectory)
            ? Directory.GetFiles(AppDataPaths.ProfilesDirectory, "*.json")
                .Select(p => Path.GetFileNameWithoutExtension(p))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

    private static bool ContainsName(List<string> names, string name)
        => names.Contains(name, StringComparer.OrdinalIgnoreCase);

    private static string GetProfilePath(string name)
        => Path.Combine(AppDataPaths.ProfilesDirectory, name + ".json");

    private static AppState? LoadState()
    {
        if (!File.Exists(AppDataPaths.StateFilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(AppDataPaths.StateFilePath);
            return JsonSerializer.Deserialize<AppState>(json, ConfigurationService.JsonOptions) ?? new AppState();
        }
        catch
        {
            return null;
        }
    }

    private static ProfileOperationResult SaveState(AppState state)
    {
        try
        {
            Directory.CreateDirectory(AppDataPaths.Root);
            File.WriteAllText(AppDataPaths.StateFilePath, JsonSerializer.Serialize(state, ConfigurationService.JsonOptions));
            return ProfileOperationResult.SuccessResult;
        }
        catch (Exception ex)
        {
            return ProfileOperationResult.Fail("Не удалось сохранить состояние приложения: " + ex.Message);
        }
    }
}