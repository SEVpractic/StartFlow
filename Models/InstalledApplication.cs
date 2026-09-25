namespace StartFlow.Models;

/// <summary>
/// Промежуточное представление приложения, зарегистрированного в Windows
/// (список shell:AppsFolder / точка входа приложения).
/// </summary>
public sealed class InstalledApplication
{
    public string Name { get; init; } = string.Empty;

    /// <summary>AppUserModelID (для Store/UWP-приложений), иначе может совпадать с именем входа.</summary>
    public string? AppUserModelId { get; init; }

    /// <summary>Путь к исполняемому файлу, если Windows предоставляет его напрямую (только для классических Win32).</summary>
    public string? ExecutablePath { get; init; }

    /// <summary>Аргументы, с которыми Windows запускает приложение по умолчанию (могут быть пустыми).</summary>
    public string? LaunchArguments { get; init; }

    /// <summary>Можно ли представить приложение текущей моделью ProgramConfig (запуск exe из пути).</summary>
    public bool CanAddAsProgram { get; init; }

    /// <summary>Причина, по которой приложение нельзя добавить (для несовместимых типов).</summary>
    public string? AddRestrictionReason { get; init; }

    /// <summary>Иконка приложения в формате BGRA (как её показывает Windows), либо null.</summary>
    public byte[]? IconData { get; init; }
}