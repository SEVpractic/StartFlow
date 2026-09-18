using System.Text.Json;
using StartFlow.Models;

namespace StartFlow.Services;

public sealed class ConfigurationService
{
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private StartFlowConfig? _config;

    public string ConfigPath { get; }

    public StartFlowConfig? CurrentConfig => _config;

    public string? LoadWarning { get; private set; }

    public ConfigurationService(string? filePath = null)
    {
        ConfigPath = filePath ?? Path.Combine(AppContext.BaseDirectory, "startflow.json");
    }

    public StartFlowConfig Load()
    {
        if (_config is not null)
        {
            return _config;
        }

        LoadWarning = null;

        if (!File.Exists(ConfigPath))
        {
            _config = StartFlowConfig.CreateDefault();
            Save(_config, out _);
            return _config;
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var loaded = JsonSerializer.Deserialize<StartFlowConfig>(json, JsonOptions);
            if (loaded is null)
            {
                throw new JsonException("Конфигурация пуста.");
            }

            _config = loaded;
            return _config;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            TryBackupCorruptFile();
            _config = StartFlowConfig.CreateDefault();
            LoadWarning = "Файл конфигурации повреждён и не может быть прочитан. Создана конфигурация по умолчанию.";
            Save(_config, out _);
            return _config;
        }
    }

    public bool Save(StartFlowConfig config, out string? error)
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions));
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            var backup = ConfigPath + ".bad";
            File.Copy(ConfigPath, backup, overwrite: true);
        }
        catch
        {
            // Backup is best-effort; ignore failures.
        }
    }
}