using System.Text.Json;

namespace HelixCoreServerCtl;

internal class ServiceManager
{
    private static readonly ServiceManager _instance = new();
    public static ServiceManager Instance => _instance;

    private readonly Dictionary<string, Service> services = new();

    private ServiceManager()
    {
        LoadServices();
    }

    private void LoadServices()
    {
        var appConfig = AppConfig.Instance;

        if (appConfig.Includes is not null)
        {
            foreach (var includePath in appConfig.Includes)
            {
                if (Path.IsPathRooted(includePath) && Directory.Exists(includePath))
                {
                    var confFilePaths = Directory.GetFiles(includePath, "*.conf");
                    foreach (var confFilePath in confFilePaths)
                    {
                        if (File.Exists(confFilePath))
                        {
                            try
                            {
                                var config = JsonSerializer.Deserialize<ServiceConfig>(File.ReadAllText(confFilePath));
                                if (config is not null)
                                {
                                    if (config.Name is not null && !services.ContainsKey(config.Name))
                                    {
                                        config.FilePath = confFilePath;
                                        services.Add(config.Name, new Service(config));
                                    }
                                }
                            }
                            catch (JsonException)
                            {
                            }
                        }
                    }
                }
            }
        }
    }

    public IEnumerable<Service> GetAllServices()
    {
        return services.Values;
    }

    public Service? FindServiceByName(string name)
    {
        return services.GetValueOrDefault(name);
    }

    public Service? CreateService(ServiceConfig config)
    {
        if (config.Name is null)
        {
            return null;
        }

        if (services.ContainsKey(config.Name))
        {
            return null;
        }

        if (config.FilePath is not null && !File.Exists(config.FilePath))
        {
            var directoryPath = Path.GetDirectoryName(config.FilePath);
            if (directoryPath is not null)
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllText(config.FilePath, JsonSerializer.Serialize<ServiceConfig>(config, new JsonSerializerOptions { WriteIndented = true }));
        }

        var service = new Service(config);
        services.Add(config.Name, service);

        return service;
    }
}
