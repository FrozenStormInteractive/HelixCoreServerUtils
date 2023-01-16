using System.Text.Json;
using System.Text.Json.Serialization;

namespace HelixCoreServerCtl;

internal class AppConfig
{
    [JsonInclude]
    [JsonPropertyName("P4DExecute")]
    public string? _P4DExecute = null;

    [JsonIgnore]
    public string? P4DExecute => this._P4DExecute ?? AppPaths.P4DExecute;

    [JsonInclude]
    [JsonPropertyName("PidFileDirectory")]
    public string? _pidFileDirectory = null;

    [JsonIgnore]
    public string? PidFileDirectory => this._pidFileDirectory ?? AppPaths.PidFileDirectory;

    [JsonInclude]
    [JsonPropertyName("DefaultServerRootDirectory")]
    public string? _defaultServerRootDirectory = null;

    [JsonIgnore]
    public string? DefaultServerRootDirectory => this._defaultServerRootDirectory ?? AppPaths.DefaultServerRootDirectory;

    public List<string>? Includes { get; set; }

    public Dictionary<string, string?>? Environment { get; set; }

    private static AppConfig? appConfig = null;

    public void Validate()
    {
        if (P4DExecute is null)
        {
            throw new Exception($"{AppPaths.ConfigFile}: Missing P4DExecute field");
        }

        if (PidFileDirectory is null)
        {
            throw new Exception($"{AppPaths.ConfigFile}: Missing PidFileDirectory field");
        }

        if (DefaultServerRootDirectory is null)
        {
            throw new Exception($"{AppPaths.ConfigFile}: Missing DefaultServerRootDirectory field");
        }
    }

    public static void InitInstance()
    {
        if (appConfig is null)
        {
            var configFilePath = AppPaths.ConfigFile;
            if (configFilePath is null)
            {
                throw new Exception($"Cannot load config");
            }

            if (File.Exists(configFilePath))
            {
                try
                {
                    var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(configFilePath));
                    if (config is not null)
                    {
                        appConfig = config;
                        return;
                    }
                }
                catch (JsonException exception)
                {
                    throw new Exception($"Cannot load config file '{configFilePath}': {exception.Message}", exception);
                }
            }
            else
            {
                throw new Exception($"Cannot load config file '{configFilePath}': No such file or directory.");
            }
        }
    }

    public static AppConfig Instance
    {
        get
        {
            if (appConfig is null)
            {
                InitInstance();
            }
            #pragma warning disable CS8603
            // Assigned in InitInstance method.
            return appConfig;
            #pragma warning restore CS8603
        }
    }
}
