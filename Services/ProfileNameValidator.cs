namespace StartFlow.Services;

/// <summary>
/// Проверка имён профилей: не допускается некорректное имя файла
/// и конфликты системных зарезервированных имён.
/// </summary>
public static class ProfileNameValidator
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "con", "prn", "aux", "nul",
        "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"
    };

    public static bool TryValidate(string? name, out string? error)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Имя профиля не может быть пустым.";
            return false;
        }

        name = name.Trim();

        if (name.Length == 0)
        {
            error = "Имя профиля не может быть пустым.";
            return false;
        }

        if (name.Length > 80)
        {
            error = "Имя профиля должно быть не длиннее 80 символов.";
            return false;
        }

        if (name is "." or "..")
        {
            error = "Некорректное имя профиля.";
            return false;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            error = "Имя профиля содержит недопустимые символы.";
            return false;
        }

        var dot = name.IndexOf('.');
        var baseName = dot >= 0 ? name[..dot] : name;
        if (ReservedNames.Contains(baseName))
        {
            error = "Имя профиля зарезервировано системой.";
            return false;
        }

        if (name.EndsWith(".", StringComparison.Ordinal) || name.EndsWith(" ", StringComparison.Ordinal))
        {
            error = "Имя профиля не должно заканчиваться точкой или пробелом.";
            return false;
        }

        error = null;
        return true;
    }
}