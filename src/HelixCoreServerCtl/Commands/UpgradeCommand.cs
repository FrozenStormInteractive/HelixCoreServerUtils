using CommandLine;

namespace HelixCoreServerCtl;

[Verb("upgrade")]
internal class UpgradeCommand : IAsyncCommand
{
    [Value(0, MetaValue = "<name>")]
    public IEnumerable<string> ServiceNames { get; set; } = null!;

    [Option('a', "all", Default = false, HelpText = "All servers.")]
    public bool AllServices { get; set; }

    public async Task<int> Execute()
    {
        IList<Service> services;
        if (AllServices)
        {
            services = ServiceManager.Instance.GetAllServices().Where(x => x.Config.Enabled).ToList();
        }
        else
        {
            List<Service> serviceList = new List<Service>();
            services = serviceList;
            foreach (var serviceName in ServiceNames)
            {
                var service = ServiceManager.Instance.FindServiceByName(serviceName);
                if (service is not null)
                {
                    if (service.Config.Enabled)
                    {
                        serviceList.Add(service);
                    }
                }
                else
                {
                    await Console.Error.WriteLineAsync($"Service '{serviceName}' not found.");
                }
            }
        }

        var tasks = new List<Task>();
        foreach (var service in services)
        {
            tasks.Add(UpgradeService(service));
        }
        await Task.WhenAll(tasks);

        Console.WriteLine($"Upgraded {services.Count} service.");

        return 0;
    }

    public async Task UpgradeService(Service service)
    {
        await service.ExecAsync("-xu");
        Console.WriteLine($"Upgraded '{service.Config.Name}' p4d service.");
    }
}
