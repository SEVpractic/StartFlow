using StartFlow.Models;

namespace StartFlow.Services;

/// <summary>
/// Миграция профилей между версиями схемы нового формата (schemaVersion).
///
/// Работает только с новым форматом профилей. Старый config.json предыдущей
/// версии StartFlow здесь не обрабатывается и не мигрируется.
///
/// Перед любой миграцией создаётся резервная копия исходного файла.
/// </summary>
public sealed class MigrationService
{
    private readonly BackupService _backup;

    public MigrationService(BackupService backup)
    {
        _backup = backup;
    }

    /// <summary>
    /// Приводит профиль к текущей версии схемы. Возвращает профиль;
    /// вызывающий обязан сохранить результат. Создаёт резервную копию
    /// исходного JSON до изменения.
    /// </summary>
    public ProfileModel Migrate(ProfileModel profile, string sourceFilePath, string rawJson)
    {
        if (profile.SchemaVersion == SchemaRegistry.CurrentSchemaVersion)
        {
            return profile;
        }

        if (profile.SchemaVersion > SchemaRegistry.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Версия схемы профиля ({profile.SchemaVersion}) новее, чем поддерживает эта версия StartFlow " +
                $"({SchemaRegistry.CurrentSchemaVersion}). Обновите StartFlow.");
        }

        var backupPath = _backup.CreateJsonBackup(sourceFilePath, profile.SchemaVersion, rawJson);

        var from = profile.SchemaVersion;
        while (from < SchemaRegistry.CurrentSchemaVersion)
        {
            if (!Migrations.TryGetValue(from, out var step))
            {
                throw new InvalidOperationException(
                    $"Нет процедуры миграции схемы из версии {from}. Исходный профиль сохранён в резервной копии: {backupPath}");
            }

            step(profile);
            from++;
            profile.SchemaVersion = from;
        }

        return profile;
    }

    /// <summary>
    /// Цепочка миграций: from version → действие.
    /// Здесь будут появляться шаги future schemaVersion 2, 3 и т.д.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, Action<ProfileModel>> Migrations =
        new Dictionary<int, Action<ProfileModel>>();
}