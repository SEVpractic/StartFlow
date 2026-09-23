using StartFlow.Services;

namespace StartFlow.Models;

/// <summary>
/// Файл профиля: оболочка с версией схемы, именем и настройками рабочего окружения.
/// </summary>
public sealed class ProfileModel
{
    public int SchemaVersion { get; set; } = SchemaRegistry.CurrentSchemaVersion;

    public string Name { get; set; } = "Default";

    public StartFlowConfig Config { get; set; } = new();

    public static ProfileModel CreateNew(string name) => new()
    {
        Name = name,
        Config = StartFlowConfig.CreateDefault()
    };
}