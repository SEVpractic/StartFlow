using System.Text.Json;
using System.Text.Json.Serialization;
using StartFlow.Services;

namespace StartFlow.Models;

public sealed class StartFlowConfig
{
    [JsonConverter(typeof(SingleOrArrayConverter<FolderGenerationConfig>))]
    public List<FolderGenerationConfig> FolderGeneration { get; set; } = new();

    public List<FolderToOpen> FoldersToOpen { get; set; } = new();

    public List<ProgramConfig> Programs { get; set; } = new();

    public string Theme { get; set; } = "default";

    public static StartFlowConfig CreateDefault() => new();
}