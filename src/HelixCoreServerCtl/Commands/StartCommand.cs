using System.Diagnostics;
using CommandLine;

namespace HelixCoreServerCtl;

[Verb("start")]
internal class StartCommand : IAsyncCommand
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
            services = ServiceManager.Instance.GetAllServices().Where(x => x.Config.Enabled && !x.IsRunning).ToList();
        }
        else
        {
            List<Service> serviceList =  new List<Service>();
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
            tasks.Add(StartService(service));
        }
        await Task.WhenAll(tasks);
        Console.WriteLine($"Started {services.Count} service.");

        return 0;
    }

    public async Task StartService(Service service)
    {
        if (service.IsRunning)
        {
            Console.WriteLine($"Started '{service.Config.Name}' p4d service.");
        }
        else
        {
            // TODO: Handle exceptions
            await service.StartAsync();
            Console.WriteLine($"Started '{service.Config.Name}' p4d service.");
        }
    }
}
