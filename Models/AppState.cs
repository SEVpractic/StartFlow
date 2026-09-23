namespace StartFlow.Models;

/// <summary>
/// Внутреннее состояние приложения. Хранится отдельно от профилей
/// и не содержит настроек рабочего окружения.
/// </summary>
public sealed class AppState
{
    public string ActiveProfile { get; set; } = "Default";
}