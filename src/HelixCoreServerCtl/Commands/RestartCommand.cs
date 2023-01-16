using System.Diagnostics;
using CommandLine;
using Mono.Unix.Native;

namespace HelixCoreServerCtl;

[Verb("restart")]
internal class RestartCommand : IAsyncCommand
{
    [Value(0, MetaValue = "<name>")]
    public IEnumerable<string> ServiceNames { get; set; } = null!;

    [Option('a', "all", Default = false, HelpText = "All servers.")]
    public bool AllServices { get; set; }

    [Option('f', "force", Default = false)]
    public bool Force { get; set; }

    public async Task<int> Execute()
    {
        IList<Service> services;
        if (AllServices)
        {
            services = ServiceManager.Instance.GetAllServices().Where(x => x.Config.Enabled && x.IsRunning).ToList();
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
                    serviceList.Add(service);
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
            tasks.Add(RestartService(service));
        }
        await Task.WhenAll(tasks);

        Console.WriteLine($"Restarted {services.Count} service.");

        // TODO

        return 0;
    }

    public async Task RestartService(Service service)
    {
        if (service.IsRunning)
        {
            await service.RestartAsync(Force);
            Console.WriteLine($"Restarted '{service.Config.Name}' p4d service.");
        }
        else
        {
            // TODO: Handle exceptions
            await service.StartAsync();
            Console.WriteLine($"'{service.Config.Name}' p4d service not running.");
        }
    }
}
