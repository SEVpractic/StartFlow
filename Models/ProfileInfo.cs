namespace StartFlow.Models;

/// <summary>
/// Отображение профиля в UI: имя и признак активности.
/// </summary>
public sealed class ProfileInfo
{
    public ProfileInfo(string name, bool isActive)
    {
        Name = name;
        IsActive = isActive;
    }

    public string Name { get; }

    public bool IsActive { get; }
}