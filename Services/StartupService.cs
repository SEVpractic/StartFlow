using System.ComponentModel;
using StartFlow.Models;

namespace StartFlow.Services;

public sealed class StartupService
{
    private readonly ConfigurationService _configService;
    private readonly FolderService _folderService;
    private readonly ProcessService _processService;

    public StartupService(
        ConfigurationService configService,
        FolderService? folderService = null,
        ProcessService? processService = null)
    {
        _configService = configService;
        _folderService = folderService ?? new FolderService();
        _processService = processService ?? new ProcessService();
    }

    public Task<StartupResult> RunAsync(StartFlowConfig? config = null)
    {
        var cfg = config ?? _configService.Load() ?? StartFlowConfig.CreateDefault();
        return Task.Run(() => Execute(cfg));
    }

    private StartupResult Execute(StartFlowConfig config)
    {
        var result = new StartupResult();

        GenerateFolder(config, result);
        OpenFolders(config, result);
        StartPrograms(config, result);

        return result;
    }

    private void GenerateFolder(StartFlowConfig config, StartupResult result)
    {
        foreach (var generation in config.FolderGeneration.Where(g => g is not null && g.Enabled))
        {
            if (string.IsNullOrWhiteSpace(generation.Path) && string.IsNullOrWhiteSpace(generation.Template))
            {
                result.AddError("Генерация папки", "Не указаны путь и шаблон.");
                continue;
            }

            try
            {
                var target = FolderService.ResolveTemplatePath(generation);
                if (string.IsNullOrWhiteSpace(target))
                {
                    result.AddError("Генерация папки", "Не удалось вычислить путь для папки.");
                    continue;
                }

                _folderService.EnsureFolderExists(target);
                result.CreatedFolders++;

                if (generation.OpenAfterCreate && _folderService.OpenInExplorer(target))
                {
                    result.OpenedFolders++;
                }
            }
            catch (Exception ex)
            {
                result.AddError("Генерация папки", $"Не удалось создать папку: {ex.Message}");
            }
        }
    }

    private void OpenFolders(StartFlowConfig config, StartupResult result)
    {
        foreach (var folder in config.FoldersToOpen.Where(f => f is not null && f.Enabled && !string.IsNullOrWhiteSpace(f.Path)))
        {
            try
            {
                if (_folderService.OpenInExplorer(folder.Path))
                {
                    result.OpenedFolders++;
                }
                else
                {
                    result.AddError("Открытие папки", $"Не удалось открыть: {folder.Path}");
                }
            }
            catch (Exception ex)
            {
                result.AddError("Открытие папки", $"Не удалось открыть {folder.Path}: {ex.Message}");
            }
        }
    }

    private void StartPrograms(StartFlowConfig config, StartupResult result)
    {
        foreach (var program in config.Programs.Where(p => p is not null && p.Enabled))
        {
            var displayName = string.IsNullOrWhiteSpace(program.Name)
                ? Path.GetFileName(program.Path)
                : program.Name;

            try
            {
                if (string.IsNullOrWhiteSpace(program.Path))
                {
                    result.AddError("Запуск", $"«{displayName}»: не указан путь.");
                    continue;
                }

                if (!File.Exists(program.Path))
                {
                    result.AddError("Запуск", $"«{displayName}»: файл не найден ({program.Path}).");
                    continue;
                }

                var status = _processService.Start(program);
                if (status == ProgramStartResult.Started)
                {
                    result.StartedPrograms++;
                }
                else
                {
                    result.SkippedPrograms++;
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                result.AddError("Запуск", $"«{displayName}»: запуск отменён пользователем.");
            }
            catch (Exception ex)
            {
                result.AddError("Запуск", $"«{displayName}»: {ex.Message}");
            }
        }
    }
}