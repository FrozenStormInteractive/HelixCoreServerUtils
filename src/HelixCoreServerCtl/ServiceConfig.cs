using System.Text.Json.Serialization;

namespace HelixCoreServerCtl;

internal class ServiceConfig
{
    [JsonIgnore]
    public string? FilePath { get; set; }

    public string? Name { get; set; }

    public string? ServerType { get; set; }

    public string? Owner { get; set; }

    public string? Execute { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Args { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Enabled { get; set; } = true;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Prefix { get; set; }

    public Dictionary<string, string?>? Environment { get; set; }
}
